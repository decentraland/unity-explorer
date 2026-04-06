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
using UnityEngine;
using Utility.Arch;

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
        private const string TAG = nameof(ProximityAudioPositionSystem);

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources;
        private readonly List<Entity> entitiesToCleanUp = new ();

        private VoiceChatConfiguration? configuration;
        private SingleInstanceEntity cameraEntity;
        private SingleInstanceEntity playerEntity;

        private Transform audioListenerTransform;
        private bool hasAudioListenerSource;

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
            AssignPendingSources();

            ref readonly CameraComponent cam = ref cameraEntity.GetCameraComponent(World);

            if(!hasAudioListenerSource)
                audioListenerTransform = CacheAudioListenerTransform(cam.Camera);

            Vector3 cameraPos = cam.Camera.transform.position;
            Vector3 localHeadPos = cameraPos;

            if (World.TryGet(playerEntity, out PlayerComponent playerComp))
                localHeadPos = playerComp.CameraFocus.position;

            SyncPositionsQuery(World, audioListenerTransform, cameraPos, localHeadPos);
            ProcessCleanUp();
        }

        private void AssignPendingSources()
        {
            foreach (KeyValuePair<string, LivekitAudioSource> kvp in activeAudioSources)
            {
                if (!entityParticipantTable.TryGet(kvp.Key, out IReadOnlyEntityParticipantTable.Entry entry))
                    continue;

                if (World.Has<ProximityAudioSourceComponent>(entry.Entity))
                {
                    ref ProximityAudioSourceComponent component = ref World.Get<ProximityAudioSourceComponent>(entry.Entity);
                    component.LivekitAudioSource = kvp.Value;
                    component.Transform = kvp.Value.transform;
                }
                else
                {
                    World.Add(entry.Entity, new ProximityAudioSourceComponent(kvp.Value));
                }
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositions([Data] Transform listenerTransform, [Data] Vector3 cameraPos, [Data] Vector3 localHeadPos,
            Entity entity, in CharacterTransform characterTransform, ref ProximityAudioSourceComponent proximityAudio)
        {
            // if (proximityAudio.Transform == null)
            // {
            //     entitiesToCleanUp.Add(entity);
            //     return;
            // }

            //  Project audio source position to proper imaginary position as it would be for listener in the head (not in camera)
            Vector3 remoteHeadPos = World.TryGet(entity, out AvatarBase? avatarBase)
                ? avatarBase.HeadAnchorPoint.position
                : characterTransform.Position + new Vector3(0f, FALLBACK_HEAD_HEIGHT, 0f);

            proximityAudio.Transform.position = cameraPos + (remoteHeadPos - localHeadPos);

            // Angles for complex panning (i.e. head shadow)
            (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, ref proximityAudio);
            proximityAudio.LivekitAudioSource.SetSpatialAngles(azimuth, elevation);
        }

        private static (float azimuth, float elevation) CalculateSpatialAngles(Transform listenerTransform, ref ProximityAudioSourceComponent proximityAudio)
        {
            Vector3 direction = proximityAudio.Transform.position - listenerTransform.position;
            Vector3 local = listenerTransform.InverseTransformDirection(direction);

            float horizontalDist = Mathf.Sqrt((local.x * local.x) + (local.z * local.z));

            float elevation = Mathf.Atan2(local.y, horizontalDist);
            float azimuth = Mathf.Atan2(local.x, local.z);

            return (azimuth, elevation);
        }

        private void ProcessCleanUp()
        {
            foreach (Entity entity in entitiesToCleanUp)
                World.TryRemove<ProximityAudioSourceComponent>(entity);

            entitiesToCleanUp.Clear();
        }

        private Transform CacheAudioListenerTransform(Camera camera)
        {
            hasAudioListenerSource = true;

            AudioListener listener = camera.GetComponent<AudioListener>();
            return listener != null ? listener.transform : camera.transform;
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
