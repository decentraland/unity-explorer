using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Components;
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
    ///     Per-frame mute enforcement: <see cref="NearbyMuteService.IsMuted"/> is queried for every audio entity
    ///     and written to <see cref="AudioSource.mute"/>. This is self-healing on toggle (no event plumbing) and
    ///     subsumes the old "first-sync unmute" hack — the binding system starts with <c>mute = true</c> so the
    ///     first successful tick recomputes it (avoids the world-origin burst between <c>Play()</c> and sync).
    ///     Carries no lifecycle responsibility — structural changes for audio entities are owned by
    ///     <see cref="NearbyAudioCleanupSystem"/>.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(NearbyAudioBindingSystem))]
    public partial class NearbyAudioPositionSystem : BaseUnityLoopSystem
    {
        // Profiler markers — fine-grained per-step instrumentation for diagnosing where the
        // per-entity main-thread cost lives. Markers are no-ops in non-development builds
        // (Unity strips Profiler.* on release), so leaving them in place is free in shipping.
        // Hierarchy mirrors the call structure: Update wraps everything; Query.* slices the
        // per-entity work. To collapse in Profiler, group by "NearbyAudio.Position".
        private static readonly ProfilerMarker UPDATE_MARKER = new ("NearbyAudio.Position.Update");
        private static readonly ProfilerMarker GET_LISTENER_MARKER = new ("NearbyAudio.Position.GetListener");
        private static readonly ProfilerMarker TRY_GET_AVATAR_MARKER = new ("NearbyAudio.Position.Query.TryGetAvatar");
        private static readonly ProfilerMarker RANGE_TAGS_MARKER = new ("NearbyAudio.Position.Query.RangeTags");
        private static readonly ProfilerMarker INACTIVE_SET_MARKER = new ("NearbyAudio.Position.Query.InactiveSetEnabled");
        private static readonly ProfilerMarker HEAD_POS_MARKER = new ("NearbyAudio.Position.Query.HeadPositionRead");
        private static readonly ProfilerMarker TRANSFORM_WRITE_MARKER = new ("NearbyAudio.Position.Query.TransformWrite");
        private static readonly ProfilerMarker SPATIAL_ANGLES_MARKER = new ("NearbyAudio.Position.Query.SpatialAngles");
        private static readonly ProfilerMarker MUTE_SYNC_MARKER = new ("NearbyAudio.Position.Query.MuteSync");

        private readonly NearbyMuteService muteService;

        private SingleInstanceEntity cameraEntity;
        private SingleInstanceEntity playerEntity;
        private bool isFirstPerson;

        internal NearbyAudioPositionSystem(World world, NearbyMuteService muteService) : base(world)
        {
            this.muteService = muteService;
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
            playerEntity = World.CachePlayer();
        }

        protected override void Update(float t)
        {
            using var _ = UPDATE_MARKER.Auto();

            // The AudioListener is on the camera, but spatial gain should be relative to the player's head.
            // In ThirdPerson these positions differ, so we track both to reproject remote sources before applying spatial audio.
            (Transform listenerTransform, Vector3 playerHeadPos) = GetListenerAndHeadPositions();
            SyncPositionsAndSpatialAnglesQuery(World, listenerTransform, playerHeadPos);
        }

        private (Transform listenerTransform, Vector3 playerHeadPos) GetListenerAndHeadPositions()
        {
            using var _ = GET_LISTENER_MARKER.Auto();

            ref readonly CameraComponent cam = ref cameraEntity.GetCameraComponent(World);
            isFirstPerson = cam.Mode == CameraMode.FirstPerson;

            Vector3 headPos = cam.Camera.transform.position;
            if (!isFirstPerson && World.TryGet(playerEntity, out PlayerComponent playerComp))
                headPos = playerComp.CameraFocus.position;

            return (cam.Camera.transform, headPos);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos, ref NearbyAudioSourceComponent nearbyAudio)
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
            bool inactive = World.Has<IsSuspendedTag>(avatar) || !World.Has<InAudibleRangeTag>(avatar);
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

            TRANSFORM_WRITE_MARKER.Begin();
            src.transform.position = sourcePos;
            TRANSFORM_WRITE_MARKER.End();

            if (!src.AudioSource.isVirtual)
            {
                SPATIAL_ANGLES_MARKER.Begin();
                (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, sourcePos);
                src.SetSpatialAngles(azimuth, elevation);
                SPATIAL_ANGLES_MARKER.End();
            }

            // Per-frame mute enforcement — self-healing on toggle, also unmutes the binding-time start-mute on first successful tick (when IsMuted is false).
            MUTE_SYNC_MARKER.Begin();
            src.AudioSource.mute = muteService.IsMuted(nearbyAudio.Key.identity);
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
