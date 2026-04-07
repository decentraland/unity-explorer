using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Multiplayer.Profiles.Systems;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming.Audio;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.VoiceChat.Proximity.Systems
{
    /// <summary>
    /// Handles positioning of Audio Sources for Proximity Voice Chat
    ///    - Assigns <see cref="ProximityAudioSourceComponent"/> to remote entities who participate in the Proximity Chat
    ///    - syncs AudioSource positions with <see cref="CharacterTransform"/> each frame, and applies spatial audio settings.
    /// <see cref="VoiceChatConfiguration"/> is the single source of truth.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    public partial class ProximityAudioPositionSystem : BaseUnityLoopSystem
    {
        private const float FALLBACK_HEAD_HEIGHT = 1.75f;

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources;

        private readonly List<Entity> entitiesToCleanUp = new (4);

        private VoiceChatConfiguration? configuration;
        private SingleInstanceEntity cameraEntity;
        private SingleInstanceEntity playerEntity;
        private bool isFirstPerson;

        internal ProximityAudioPositionSystem(World world,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.activeAudioSources = activeAudioSources;
        }

        internal void SetConfiguration(VoiceChatConfiguration config) => configuration = config;

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
            playerEntity = World.CachePlayer();
        }

        protected override void Update(float t)
        {
            entitiesToCleanUp.Clear();

            AssignPendingSources();

            (Transform listenerTransform, Vector3 playerHeadPos) = GetListenerAndHeadPositions();
            SyncPositionsAndSpatialAnglesQuery(World, listenerTransform, playerHeadPos);

            foreach (Entity entity in entitiesToCleanUp)
                World.Remove<ProximityAudioSourceComponent>(entity);
        }

        private void AssignPendingSources()
        {
            foreach (KeyValuePair<string, LivekitAudioSource> kvp in activeAudioSources)
            {
                if (!entityParticipantTable.TryGet(kvp.Key, out IReadOnlyEntityParticipantTable.Entry entry)) continue;
                if (World.Has<DeleteEntityIntention>(entry.Entity)) continue;

                if (World.Has<ProximityAudioSourceComponent>(entry.Entity))
                {
                    ref ProximityAudioSourceComponent component = ref World.Get<ProximityAudioSourceComponent>(entry.Entity);
                    component.LivekitAudioSource = kvp.Value;
                    component.Transform = kvp.Value.transform;
                }
                else
                {
                    World.Add(entry.Entity, new ProximityAudioSourceComponent(kvp.Key, kvp.Value));
                }
            }
        }

        private (Transform listenerTransform, Vector3 playerHeadPos) GetListenerAndHeadPositions()
        {
            ref readonly CameraComponent cam = ref cameraEntity.GetCameraComponent(World);
            isFirstPerson = cam.Mode == CameraMode.FirstPerson;

            Vector3 headPos = cam.Camera.transform.position;
            if (!isFirstPerson && World.TryGet(playerEntity, out PlayerComponent playerComp))
                headPos = playerComp.CameraFocus.position;

            return (cam.Camera.transform, headPos);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos,
            Entity entity, in CharacterTransform characterTransform, ref ProximityAudioSourceComponent proximityAudio)
        {
            if (!activeAudioSources.ContainsKey(proximityAudio.ParticipantIdentity))
            {
                entitiesToCleanUp.Add(entity);
                return;
            }

            Vector3 remoteAvatarHeadPos = World.TryGet(entity, out AvatarBase? avatarBase)
                ? avatarBase.HeadAnchorPoint.position
                : characterTransform.Position + new Vector3(0f, FALLBACK_HEAD_HEIGHT, 0f);

            // reprojection, so gain is calculated relative to the head and not the camera position (audioListener is on the camera)
            Vector3 sourcePos = isFirstPerson ? remoteAvatarHeadPos : listenerTransform.position + (remoteAvatarHeadPos - playerHeadPos);
            proximityAudio.Transform.position = sourcePos;

            (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, sourcePos);
            proximityAudio.LivekitAudioSource.SetSpatialAngles(azimuth, elevation);
        }

        private static (float azimuth, float elevation) CalculateSpatialAngles(Transform listenerTransform, Vector3 sourcePosition)
        {
            Vector3 local = listenerTransform.InverseTransformPoint(sourcePosition);

            float horizontalDist = math.sqrt((local.x * local.x) + (local.z * local.z));
            float elevation = math.atan2(local.y, horizontalDist);

            float azimuth = math.atan2(local.x, local.z);

            return (azimuth, elevation);
        }

        // [Query]
        // [None(typeof(DeleteEntityIntention))]
        // private void ApplySettings(ref ProximityAudioSourceComponent proximityAudio)
        // {
        //     if (proximityAudio.AudioSource != null)
        //         configuration?.ApplyProximitySettingsTo(proximityAudio.AudioSource);
        // }
    }
}
