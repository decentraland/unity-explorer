using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming.Audio;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Reads position from the avatar entity referenced by <see cref="NearbyAudioSourceComponent.AvatarEntity"/>
    ///     and drives the <see cref="LivekitAudioSource"/> transform + spatial angles each frame,
    ///     applying the speaker's and listener's <see cref="VoiceChatAreaModifiersComponent"/>.
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateAfter(typeof(NearbyAudioBindingSystem))]
    public partial class NearbyAudioPositionSystem : BaseUnityLoopSystem
    {
        private const float POSITION_EPSILON_SQR = 0.01f;
        private static readonly VoiceChatAreaModifiersComponent NO_AREAS = new (new List<VoiceChatAreaModifier>(0));

        private readonly NearbyMuteService muteService;
        private readonly NearbyListenerState listenerState;
        private readonly Entity playerEntity;
        private readonly float rolloffDistance;

        internal NearbyAudioPositionSystem(World world, NearbyMuteService muteService, NearbyListenerState listenerState, Entity playerEntity, VoiceChatConfiguration configuration) : base(world)
        {
            this.muteService = muteService;
            this.listenerState = listenerState;
            this.playerEntity = playerEntity;
            rolloffDistance = configuration.NearbyRolloffMaxDistance;
        }

        protected override void Update(float t)
        {
            VoiceChatAreaModifiersComponent listenerAreas = World.TryGet(playerEntity, out VoiceChatAreaModifiersComponent listenerModifiers) ? listenerModifiers : NO_AREAS;
            SyncPositionsAndSpatialAnglesQuery(World, listenerState.ListenerTransform, listenerState.PlayerHeadPosition, listenerAreas);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos, [Data] in VoiceChatAreaModifiersComponent listenerAreas, ref NearbyAudioSourceComponent nearbyAudio)
        {
            Entity avatar = nearbyAudio.AvatarEntity;

            if (!World.TryGet(avatar, out AvatarBase? avatarBase)) return;

            VoiceChatAreaModifiersComponent speakerAreas = World.TryGet(avatar, out VoiceChatAreaModifiersComponent areas) ? areas : NO_AREAS;

            bool inactive = !World.TryGet(avatar, out InAudibleRangeTag rangeTag) || rangeTag.IsSuspended
                            || speakerAreas.Muted || !VoiceChatAreaModifiersComponent.CanHear(speakerAreas, listenerAreas);

            LivekitAudioSource src = nearbyAudio.LivekitAudioSource;

            if (inactive != nearbyAudio.LastInactive)
            {
                if (inactive) src.AudioSource.Stop();
                else src.AudioSource.Play();
                nearbyAudio.LastInactive = inactive;
            }

            if (inactive) return;

            Vector3 headOffset = avatarBase!.HeadAnchorPoint.position - playerHeadPos;
            float maxDistance = speakerAreas.MaxDistance;

            if (maxDistance < rolloffDistance)
                headOffset *= rolloffDistance / maxDistance;

            Vector3 sourcePos = listenerTransform.position + headOffset;

            if ((sourcePos - nearbyAudio.LastWrittenPos).sqrMagnitude > POSITION_EPSILON_SQR)
            {
                src.transform.position = sourcePos;
                nearbyAudio.LastWrittenPos = sourcePos;
            }

            if (!src.AudioSource.isVirtual)
            {
                (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, sourcePos);
                src.SetSpatialAngles(azimuth, elevation);
            }

            float volume = speakerAreas.VolumeScale;

            if (!Mathf.Approximately(volume, nearbyAudio.LastAppliedVolume))
            {
                src.AudioSource.volume = volume;
                nearbyAudio.LastAppliedVolume = volume;
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
