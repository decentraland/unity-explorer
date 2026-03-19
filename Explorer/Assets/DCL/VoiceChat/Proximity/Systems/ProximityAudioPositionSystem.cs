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
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Assigns <see cref="ProximityAudioSourceComponent"/> to remote entities whose audio
    /// sources are registered in the shared dictionary, syncs AudioSource positions with
    /// <see cref="CharacterTransform"/> each frame, and applies spatial audio settings.
    /// <see cref="VoiceChatConfiguration"/> is the single source of truth.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    public partial class ProximityAudioPositionSystem : BaseUnityLoopSystem
    {
        private const float FALLBACK_HEAD_HEIGHT = 1.75f;

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ProximityConfigHolder configHolder;
        private readonly List<Entity> entitiesToCleanUp = new ();

        private SingleInstanceEntity cameraEntity;
        private SingleInstanceEntity playerEntity;

        internal ProximityAudioPositionSystem(
            World world,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ConcurrentDictionary<string, AudioSource> activeAudioSources,
            ProximityConfigHolder configHolder) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.activeAudioSources = activeAudioSources;
            this.configHolder = configHolder;
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
            playerEntity = World.CachePlayer();
        }

        protected override void Update(float t)
        {
            if (configHolder.Config == null) return;

            AssignPendingSources();

            ref readonly CameraComponent cam = ref cameraEntity.GetCameraComponent(World);
            Vector3 cameraPos = cam.Camera.transform.position;
            Vector3 localHeadPos = cameraPos;

            if (World.TryGet(playerEntity, out PlayerComponent playerComp) && playerComp.CameraFocus != null)
                localHeadPos = playerComp.CameraFocus.position;

            SyncPositionsQuery(World, cameraPos, localHeadPos);

            ProcessCleanUp();
        }

        private void AssignPendingSources()
        {
            foreach (KeyValuePair<string, AudioSource> kvp in activeAudioSources)
            {
                if (!entityParticipantTable.TryGet(kvp.Key, out IReadOnlyEntityParticipantTable.Entry entry))
                    continue;

                if (World.Has<ProximityAudioSourceComponent>(entry.Entity))
                {
                    ref ProximityAudioSourceComponent component = ref World.Get<ProximityAudioSourceComponent>(entry.Entity);
                    component.AudioSource = kvp.Value;
                    component.AudioSourceTransform = kvp.Value != null ? kvp.Value.transform : null;
                }
                else
                {
                    World.Add(entry.Entity, new ProximityAudioSourceComponent(kvp.Value));
                }
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositions(
            [Data] Vector3 cameraPos,
            [Data] Vector3 localHeadPos,
            Entity entity,
            in CharacterTransform characterTransform,
            ref ProximityAudioSourceComponent proximityAudio)
        {
            if (proximityAudio.AudioSourceTransform == null)
            {
                entitiesToCleanUp.Add(entity);
                return;
            }

            Vector3 remoteHeadPos = World.TryGet(entity, out AvatarBase? avatarBase)
                                    && avatarBase != null
                                    && avatarBase.HeadAnchorPoint != null
                ? avatarBase.HeadAnchorPoint.position
                : characterTransform.Position + new Vector3(0f, FALLBACK_HEAD_HEIGHT, 0f);

            proximityAudio.AudioSourceTransform.position = cameraPos + (remoteHeadPos - localHeadPos);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySettings(ref ProximityAudioSourceComponent proximityAudio)
        {
            if (proximityAudio.AudioSource != null)
                configHolder.Config!.ApplyProximitySettingsTo(proximityAudio.AudioSource);
        }

        private void ProcessCleanUp()
        {
            foreach (Entity entity in entitiesToCleanUp)
            {
                if (World.Has<ProximityAudioSourceComponent>(entity))
                    World.Remove<ProximityAudioSourceComponent>(entity);
            }

            entitiesToCleanUp.Clear();
        }
    }
}
