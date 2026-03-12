using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
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
    /// A dedicated "Proximity Audio" debug widget provides runtime sliders that write
    /// directly to the SO via <see cref="ElementBinding{T}.OnValueChanged"/> callbacks.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    public partial class ProximityAudioPositionSystem : BaseUnityLoopSystem
    {
        private const float FALLBACK_HEAD_HEIGHT = 1.75f;

        private static readonly int MOUTH_TEX_ARR = Shader.PropertyToID("_MainTexArr");
        private static readonly int MOUTH_TEX_ARR_ID = Shader.PropertyToID("_MainTexArr_ID");

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ProximityConfigHolder configHolder;
        private readonly List<Entity> entitiesToCleanUp = new ();
        private readonly MaterialPropertyBlock lipSyncPropertyBlock = new ();

        private SingleInstanceEntity cameraEntity;
        private SingleInstanceEntity playerEntity;

        internal ProximityAudioPositionSystem(
            World world,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ConcurrentDictionary<string, AudioSource> activeAudioSources,
            ProximityConfigHolder configHolder,
            IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.activeAudioSources = activeAudioSources;
            this.configHolder = configHolder;

            var spatialBlendBinding = new ElementBinding<float>(1f,
                evt => { if (configHolder.Config != null) configHolder.Config.ProximitySpatialBlend = evt.newValue; });

            var dopplerBinding = new ElementBinding<float>(0f,
                evt => { if (configHolder.Config != null) configHolder.Config.ProximityDopplerLevel = evt.newValue; });

            var minDistanceBinding = new ElementBinding<float>(2f,
                evt => { if (configHolder.Config != null) configHolder.Config.ProximityMinDistance = evt.newValue; });

            var maxDistanceBinding = new ElementBinding<float>(16f,
                evt => { if (configHolder.Config != null) configHolder.Config.ProximityMaxDistance = evt.newValue; });

            var spreadBinding = new ElementBinding<float>(0f,
                evt => { if (configHolder.Config != null) configHolder.Config.ProximitySpread = evt.newValue; });

            var rolloffBinding = new EnumElementBinding<AudioRolloffMode>(
                AudioRolloffMode.Custom,
                onValueChange: mode => { if (configHolder.Config != null) configHolder.Config.ProximityRolloffMode = mode; });

            debugBuilder.TryAddWidget("Proximity Audio")
                       ?.AddFloatSliderField("Spatial Blend", spatialBlendBinding, 0f, 1f)
                        .AddFloatSliderField("Doppler Level", dopplerBinding, 0f, 5f)
                        .AddFloatSliderField("Min Distance", minDistanceBinding, 0f, 100f)
                        .AddFloatSliderField("Max Distance", maxDistanceBinding, 1f, 500f)
                        .AddFloatSliderField("Spread", spreadBinding, 0f, 360f)
                        .AddControl(new DebugDropdownDef(rolloffBinding, "Rolloff Mode"), null);
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
            ApplySettingsQuery(World);
            SetupAndUpdateLipSync(t);
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

            // Place AudioSource on the camera→remote ray at head-to-head distance.
            // Preserves direction from camera for pan while giving Unity the correct distance for rolloff.
            proximityAudio.AudioSourceTransform.position = cameraPos + (remoteHeadPos - localHeadPos);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySettings(ref ProximityAudioSourceComponent proximityAudio)
        {
            if (proximityAudio.AudioSource != null)
                configHolder.Config!.ApplyProximitySettingsTo(proximityAudio.AudioSource);
        }

        private void SetupAndUpdateLipSync(float dt)
        {
            Texture2DArray mouthTextures = configHolder.MouthTextureArray;
            if (mouthTextures == null) return;

            VoiceChatConfiguration config = configHolder.Config!;
            int idlePose = config.LipSyncIdlePoseIndex;
            int numPoses = mouthTextures.depth;
            float holdDuration = config.LipSyncPoseHoldDuration;
            HashSet<string> speakingSet = configHolder.SpeakingParticipants;

            foreach (KeyValuePair<string, AudioSource> kvp in activeAudioSources)
            {
                if (!entityParticipantTable.TryGet(kvp.Key, out IReadOnlyEntityParticipantTable.Entry entry))
                    continue;

                Entity entity = entry.Entity;

                if (World.Has<DeleteEntityIntention>(entity))
                    continue;

                if (!World.Has<ProximityLipSyncComponent>(entity))
                {
                    TrySetupLipSync(entity, idlePose);
                    continue;
                }

                ref ProximityLipSyncComponent lipSync = ref World.Get<ProximityLipSyncComponent>(entity);

                if (lipSync.MouthRenderer == null)
                {
                    entitiesToCleanUp.Add(entity);
                    continue;
                }

                bool isSpeaking = speakingSet.Contains(kvp.Key);

                if (isSpeaking)
                {
                    lipSync.PoseHoldTimer -= dt;

                    if (lipSync.PoseHoldTimer <= 0f)
                    {
                        lipSync.CurrentPoseIndex = (lipSync.CurrentPoseIndex + Random.Range(1, numPoses)) % numPoses;

                        if (lipSync.CurrentPoseIndex == idlePose)
                            lipSync.CurrentPoseIndex = (lipSync.CurrentPoseIndex + 1) % numPoses;

                        lipSync.PoseHoldTimer = holdDuration;
                    }
                }
                else
                {
                    lipSync.CurrentPoseIndex = idlePose;
                    lipSync.PoseHoldTimer = 0f;
                }

                ApplyMouthPose(ref lipSync, mouthTextures);
            }
        }

        private void TrySetupLipSync(Entity entity, int idlePose)
        {
            Renderer mouthRenderer = FindMouthRendererForEntity(entity);
            if (mouthRenderer == null) return;

            World.Add(entity, new ProximityLipSyncComponent
            {
                MouthRenderer = mouthRenderer,
                CurrentPoseIndex = idlePose,
                PoseHoldTimer = 0f,
            });
        }

        private Renderer FindMouthRendererForEntity(Entity entity)
        {
            ref readonly AvatarShapeComponent avatarShape =
                ref World.TryGetRef<AvatarShapeComponent>(entity, out bool hasAvatar);

            if (!hasAvatar) return null;

            for (int i = 0; i < avatarShape.InstantiatedWearables.Count; i++)
            {
                List<Renderer> renderers = avatarShape.InstantiatedWearables[i].Renderers;

                for (int j = 0; j < renderers.Count; j++)
                {
                    if (renderers[j] != null && renderers[j].name.EndsWith("Mask_Mouth"))
                        return renderers[j];
                }
            }

            return null;
        }

        private void ApplyMouthPose(ref ProximityLipSyncComponent lipSync, Texture2DArray mouthTextures)
        {
            lipSyncPropertyBlock.SetFloat(MOUTH_TEX_ARR_ID, lipSync.CurrentPoseIndex);
            lipSyncPropertyBlock.SetTexture(MOUTH_TEX_ARR, mouthTextures);
            lipSync.MouthRenderer.SetPropertyBlock(lipSyncPropertyBlock);
        }

        private void ProcessCleanUp()
        {
            foreach (Entity entity in entitiesToCleanUp)
            {
                Renderer mouthRenderer = World.Has<ProximityLipSyncComponent>(entity)
                    ? World.Get<ProximityLipSyncComponent>(entity).MouthRenderer
                    : null;

                if (World.Has<ProximityAudioSourceComponent>(entity))
                    World.Remove<ProximityAudioSourceComponent>(entity);

                if (World.Has<ProximityLipSyncComponent>(entity))
                    World.Remove<ProximityLipSyncComponent>(entity);

                if (mouthRenderer != null)
                {
                    lipSyncPropertyBlock.Clear();
                    mouthRenderer.SetPropertyBlock(lipSyncPropertyBlock);
                }
            }

            entitiesToCleanUp.Clear();
        }
    }
}
