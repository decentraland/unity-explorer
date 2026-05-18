using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming.Audio;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Drives the <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
    ///     Reads <see cref="AvatarBase"/> from the same entity that carries the audio-source component
    ///     (co-located after the slice-4 collapse — no cross-entity hop).
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateAfter(typeof(NearbyAudioBindingSystem))]
    public partial class NearbyAudioPositionSystem : BaseUnityLoopSystem
    {
        private const float POSITION_EPSILON_SQR = 0.01f;

        private readonly NearbyMuteService muteService;
        private readonly NearbyListenerState listenerState;

        internal NearbyAudioPositionSystem(World world, NearbyMuteService muteService, NearbyListenerState listenerState) : base(world)
        {
            this.muteService = muteService;
            this.listenerState = listenerState;
        }

        protected override void Update(float t)
        {
            SyncPositionsAndSpatialAnglesQuery(World, listenerState.ListenerTransform, listenerState.PlayerHeadPosition);
        }

        [Query]
        [All(typeof(NearbyAudioStreamerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos, Entity entity, in AvatarBase avatarBase, ref NearbyAudioSourceComponent nearbyAudio)
        {
            // Per-frame idempotent inactive-state application — self-healing.
            bool inactive = !World.TryGet(entity, out InAudibleRangeTag rangeTag) || rangeTag.IsSuspended;

            LivekitAudioSource src = nearbyAudio.LivekitAudioSource;

            // Diff-write: Stop/Play is a state change on the AudioSource voice slot, not a topology change to the DSP graph
            // Pessimistic init (LastInactive=false matches factory's enabled=true hand-off) forces the first-tick write when an avatar binds directly into the suspend band.
            if (inactive != nearbyAudio.LastInactive)
            {
                if (inactive) src.AudioSource.Stop();
                else src.AudioSource.Play();
                nearbyAudio.LastInactive = inactive;
            }

            if (inactive) return;

            // reprojection, so gain is calculated relative to the head and not the camera position (audioListener is on the camera)
            Vector3 remoteAvatarHeadPos = avatarBase.HeadAnchorPoint.position;
            Vector3 sourcePos = listenerTransform.position + (remoteAvatarHeadPos - playerHeadPos);

            if ((sourcePos - nearbyAudio.LastWrittenPos).sqrMagnitude > POSITION_EPSILON_SQR)
            {
                src.transform.position = sourcePos;
                nearbyAudio.LastWrittenPos = sourcePos;
            }

            // virtualized sources are not audible, so no need to calculate spatialization
            if (!src.AudioSource.isVirtual)
            {
                (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, sourcePos);
                src.SetSpatialAngles(azimuth, elevation);
            }

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
