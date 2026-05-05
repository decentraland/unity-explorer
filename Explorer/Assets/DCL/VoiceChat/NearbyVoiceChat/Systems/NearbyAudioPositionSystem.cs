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
    ///     Reads position from the avatar entity referenced by <see cref="NearbyAudioSourceComponent.AvatarEntity"/>
    ///     and drives the <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
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
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos, ref NearbyAudioSourceComponent nearbyAudio)
        {
            // Stale avatar entity reference — NearbyAudioCleanupSystem will tear this audio entity down in CleanUpGroup.
            bool hasAvatar = World.TryGet(nearbyAudio.AvatarEntity, out AvatarBase? avatarBase);
            if (!hasAvatar) return;

            // Per-frame idempotent inactive-state application — self-healing
            Entity avatar = nearbyAudio.AvatarEntity;
            bool inactive = !World.TryGet(avatar, out InAudibleRangeTag rangeTag) || rangeTag.IsSuspended;

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

            Vector3 remoteAvatarHeadPos = avatarBase!.HeadAnchorPoint.position;
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
