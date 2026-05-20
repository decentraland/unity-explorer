using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming.Audio;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Drives the <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
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
            StopPlayingOutOfRangeSourcesQuery(World);
            SpatializeActiveSourceQuery(World, listenerState.ListenerTransform, listenerState.PlayerHeadPosition);
        }

        [Query]
        [All(typeof(NearbyAudioStreamerComponent))]
        [None(typeof(InAudibleRangeTag), typeof(DeleteEntityIntention))]
        private void StopPlayingOutOfRangeSources(ref NearbyAudioSourceComponent nearbyAudio)
        {
            StopIfPlaying(ref nearbyAudio);
        }

        [Query]
        [All(typeof(NearbyAudioStreamerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void SpatializeActiveSource([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos, in InAudibleRangeTag rangeTag, in AvatarBase avatarBase, ref NearbyAudioSourceComponent nearbyAudio)
        {
            if (rangeTag.IsSuspended)
            {
                StopIfPlaying(ref nearbyAudio);
                return;
            }

            LivekitAudioSource src = nearbyAudio.LivekitAudioSource;

            // Diff-write: Stop/Play is a state change on the AudioSource voice slot, not a topology change to the DSP graph.
            // Pessimistic init (LastInactive=false matches factory's enabled=true hand-off) forces the first-tick write when an avatar binds directly into the suspend band.
            if (nearbyAudio.LastInactive)
            {
                src.AudioSource.Play();
                nearbyAudio.LastInactive = false;
            }

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

            UpdateMute(ref nearbyAudio, src);
        }

        private static void StopIfPlaying(ref NearbyAudioSourceComponent nearbyAudio)
        {
            if (nearbyAudio.LastInactive) return;

            nearbyAudio.LivekitAudioSource.AudioSource.Stop();
            nearbyAudio.LastInactive = true;
        }

        private static (float azimuth, float elevation) CalculateSpatialAngles(Transform listenerTransform, Vector3 sourcePosition)
        {
            Vector3 local = listenerTransform.InverseTransformPoint(sourcePosition);

            float horizontalDist = math.sqrt((local.x * local.x) + (local.z * local.z));
            float elevation = math.atan2(local.y, horizontalDist);

            float azimuth = math.atan2(local.x, local.z);

            return (azimuth, elevation);
        }

        private void UpdateMute(ref NearbyAudioSourceComponent nearbyAudio, LivekitAudioSource src)
        {
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
    }
}
