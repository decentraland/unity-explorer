using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming.Audio;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Reads position from the avatar entity referenced by <see cref="NearbyAudioSourceComponent.AvatarEntity"/>
    ///     and drives the <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
    ///     Per-frame mute enforcement: gated by <see cref="NearbyMuteService.CacheVersion"/> — the per-entity
    ///     <see cref="NearbyAudioSourceComponent.LastSeenMuteVersion"/> compares against the cache version, and the
    ///     <see cref="AudioSource.mute"/> interop write only happens when both the cache changed and the per-entity
    ///     value differs from <see cref="NearbyAudioSourceComponent.LastAppliedMute"/>. Self-healing on toggle is
    ///     preserved: the system still visits every entity every frame, but the hot work is skipped while the cache
    ///     is unchanged. The component's pessimistic init (LastSeenMuteVersion=0, cache.Version=1) guarantees the
    ///     world-origin-burst-protection recompute on the first tick after binding.
    ///     Per-frame transform write is similarly gated by a sqrMagnitude epsilon — angle math still uses the
    ///     freshly-computed sourcePos every frame, so azimuth/elevation never desync.
    ///     Carries no lifecycle responsibility — structural changes for audio entities are owned by
    ///     <see cref="NearbyAudioCleanupSystem"/>.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(NearbyAudioBindingSystem))]
    public partial class NearbyAudioPositionSystem : BaseUnityLoopSystem
    {
        // sqr(0.1m) — skip Transform.position writes when the per-frame delta is below ~10 cm.
        // Spatial gain at the 22 m audible-range cap is far below human-perceptible level at this delta;
        // angle math still uses the freshly-computed sourcePos every frame, so azimuth/elevation never desync.
        private const float POSITION_EPSILON_SQR = 0.01f;

        // Profiler markers — fine-grained per-step instrumentation for diagnosing where the
        // per-entity main-thread cost lives. Markers are no-ops in non-development builds
        // (Unity strips Profiler.* on release), so leaving them in place is free in shipping.
        // Hierarchy mirrors the call structure: Update wraps everything; Query.* slices the
        // per-entity work. To collapse in Profiler, group by "NearbyAudio.Position".
        private static readonly ProfilerMarker UPDATE_MARKER = new ("NearbyAudio.Position.Update");
        private static readonly ProfilerMarker TRY_GET_AVATAR_MARKER = new ("NearbyAudio.Position.Query.TryGetAvatar");
        private static readonly ProfilerMarker RANGE_TAGS_MARKER = new ("NearbyAudio.Position.Query.RangeTags");
        private static readonly ProfilerMarker INACTIVE_SET_MARKER = new ("NearbyAudio.Position.Query.InactiveSetEnabled");
        private static readonly ProfilerMarker HEAD_POS_MARKER = new ("NearbyAudio.Position.Query.HeadPositionRead");
        private static readonly ProfilerMarker TRANSFORM_WRITE_MARKER = new ("NearbyAudio.Position.Query.TransformWrite");
        private static readonly ProfilerMarker SPATIAL_ANGLES_MARKER = new ("NearbyAudio.Position.Query.SpatialAngles");
        private static readonly ProfilerMarker MUTE_SYNC_MARKER = new ("NearbyAudio.Position.Query.MuteSync");

        private readonly NearbyMuteService muteService;

        private SingleInstanceEntity cameraEntity;

        internal NearbyAudioPositionSystem(World world, NearbyMuteService muteService) : base(world)
        {
            this.muteService = muteService;
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            using var _ = UPDATE_MARKER.Auto();

            // Listener data is produced once per tick by NearbyAudibleRangeSystem on the camera entity.
            // The AudioListener is on the camera, but spatial gain should be relative to the player's
            // head — in ThirdPerson the two diverge, so we reproject remote sources before applying
            // spatial audio. Single chunk lookup; no PlayerComponent query needed here.
            ref readonly NearbyListenerComponent listener = ref World.Get<NearbyListenerComponent>(cameraEntity);
            SyncPositionsAndSpatialAnglesQuery(World, listener.ListenerTransform, listener.PlayerHeadPosition, listener.IsFirstPerson);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos, [Data] bool isFirstPerson, ref NearbyAudioSourceComponent nearbyAudio)
        {
            // Stale avatar entity reference — NearbyAudioCleanupSystem will tear this audio entity down in CleanUpGroup.
            TRY_GET_AVATAR_MARKER.Begin();
            bool hasAvatar = World.TryGet(nearbyAudio.AvatarEntity, out AvatarBase? avatarBase);
            TRY_GET_AVATAR_MARKER.End();
            if (!hasAvatar) return;

            // Per-frame idempotent inactive-state application — self-healing, same pattern as `mute`.
            // "inactive" covers both suspend (16–22 m band) and the one-frame transient between an avatar
            // crossing 22 m outward (RangeMarker drops InAudibleRangeTag in AvatarGroup) and Cleanup
            // dooming this audio entity in the later CleanUpGroup. Without the range-absence clause,
            // PositionSystem would run the full spatial pipeline once on a doomed entity.
            RANGE_TAGS_MARKER.Begin();
            Entity avatar = nearbyAudio.AvatarEntity;
            bool inactive = !World.TryGet(avatar, out InAudibleRangeTag rangeTag) || rangeTag.IsSuspended;
            RANGE_TAGS_MARKER.End();

            LivekitAudioSource src = nearbyAudio.LivekitAudioSource;
            INACTIVE_SET_MARKER.Begin();
            src.enabled = !inactive;
            src.AudioSource.enabled = !inactive;
            INACTIVE_SET_MARKER.End();

            if (inactive) return;

            HEAD_POS_MARKER.Begin();
            Vector3 remoteAvatarHeadPos = avatarBase!.HeadAnchorPoint.position;

            // reprojection, so gain is calculated relative to the head and not the camera position (audioListener is on the camera)
            Vector3 sourcePos = isFirstPerson ? remoteAvatarHeadPos : listenerTransform.position + (remoteAvatarHeadPos - playerHeadPos);
            HEAD_POS_MARKER.End();

            // Diff-write: skip native transform.position interop while the avatar barely moved. Spatial-angle math below
            // still feeds off the fresh sourcePos so audibility direction never desyncs from the actual head position.
            TRANSFORM_WRITE_MARKER.Begin();
            if ((sourcePos - nearbyAudio.LastWrittenPos).sqrMagnitude > POSITION_EPSILON_SQR)
            {
                src.transform.position = sourcePos;
                nearbyAudio.LastWrittenPos = sourcePos;
            }
            TRANSFORM_WRITE_MARKER.End();

            if (!src.AudioSource.isVirtual)
            {
                SPATIAL_ANGLES_MARKER.Begin();
                (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, sourcePos);
                src.SetSpatialAngles(azimuth, elevation);
                SPATIAL_ANGLES_MARKER.End();
            }

            // Mute version + diff-write: hot path is a single uint compare. Lookup and AudioSource.mute interop only
            // run on ticks where the cache mutated; AudioSource.mute is set only if this entity's value actually changed.
            // Pessimistic init (component LastSeenMuteVersion=0, cache.Version starts at 1) forces the binding-time
            // unmute recompute on the first tick — preserves the world-origin-burst protection from before the diff layer.
            MUTE_SYNC_MARKER.Begin();
            uint cacheVersion = muteService.CacheVersion;
            if (nearbyAudio.LastSeenMuteVersion != cacheVersion)
            {
                bool muted = muteService.IsMuted(nearbyAudio.Key.identity);
                if (muted != nearbyAudio.LastAppliedMute)
                {
                    src.AudioSource.mute = muted;
                    nearbyAudio.LastAppliedMute = muted;
                }
                nearbyAudio.LastSeenMuteVersion = cacheVersion;
            }
            MUTE_SYNC_MARKER.End();
        }

        private static (float azimuth, float elevation) CalculateSpatialAngles(Transform listenerTransform, Vector3 sourcePosition)
        {
            Vector3 local = listenerTransform.InverseTransformPoint(sourcePosition);

            float horizontalDist = math.sqrt((local.x * local.x) + (local.z * local.z));
            float elevation = math.atan2(local.y, horizontalDist);

            float azimuth = math.atan2(local.x, local.z);

            return (azimuth, elevation);
        }
    }
}
