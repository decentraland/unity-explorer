using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
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
    /// <see cref="CharacterTransform"/> each frame, and applies spatial audio settings changes.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    public partial class ProximityAudioPositionSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ProximityAudioSettings audioSettings;
        private readonly List<Entity> entitiesToCleanUp = new ();

        private readonly ElementBinding<float> spatialBlendBinding;
        private readonly ElementBinding<float> dopplerBinding;
        private readonly ElementBinding<float> minDistanceBinding;
        private readonly ElementBinding<float> maxDistanceBinding;
        private readonly ElementBinding<float> spreadBinding;

        internal ProximityAudioPositionSystem(
            World world,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ConcurrentDictionary<string, AudioSource> activeAudioSources,
            ProximityAudioSettings audioSettings,
            IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.activeAudioSources = activeAudioSources;
            this.audioSettings = audioSettings;

            spatialBlendBinding = new ElementBinding<float>(audioSettings.SpatialBlend);
            dopplerBinding = new ElementBinding<float>(audioSettings.DopplerLevel);
            minDistanceBinding = new ElementBinding<float>(audioSettings.MinDistance);
            maxDistanceBinding = new ElementBinding<float>(audioSettings.MaxDistance);
            spreadBinding = new ElementBinding<float>(audioSettings.Spread);

            debugBuilder.TryAddWidget("Proximity Audio")
                       ?.AddFloatSliderField("Spatial Blend", spatialBlendBinding, 0f, 1f)
                        .AddFloatSliderField("Doppler Level", dopplerBinding, 0f, 5f)
                        .AddFloatSliderField("Min Distance", minDistanceBinding, 0f, 100f)
                        .AddFloatSliderField("Max Distance", maxDistanceBinding, 1f, 500f)
                        .AddFloatSliderField("Spread", spreadBinding, 0f, 360f);
        }

        protected override void Update(float t)
        {
            if (audioSettings.SyncFromConfig())
                PushSettingsToBindings();

            ReadDebugValues();
            AssignPendingSources();
            SyncPositionsQuery(World);

            if (audioSettings.IsDirty)
            {
                ApplySettingsQuery(World);
                audioSettings.IsDirty = false;
            }

            ProcessCleanUp();
        }

        private void PushSettingsToBindings()
        {
            spatialBlendBinding.Value = audioSettings.SpatialBlend;
            dopplerBinding.Value = audioSettings.DopplerLevel;
            minDistanceBinding.Value = audioSettings.MinDistance;
            maxDistanceBinding.Value = audioSettings.MaxDistance;
            spreadBinding.Value = audioSettings.Spread;
        }

        private void ReadDebugValues()
        {
            bool changed = false;

            if (!Mathf.Approximately(audioSettings.SpatialBlend, spatialBlendBinding.Value))
            { audioSettings.SpatialBlend = spatialBlendBinding.Value; changed = true; }

            if (!Mathf.Approximately(audioSettings.DopplerLevel, dopplerBinding.Value))
            { audioSettings.DopplerLevel = dopplerBinding.Value; changed = true; }

            if (!Mathf.Approximately(audioSettings.MinDistance, minDistanceBinding.Value))
            { audioSettings.MinDistance = minDistanceBinding.Value; changed = true; }

            if (!Mathf.Approximately(audioSettings.MaxDistance, maxDistanceBinding.Value))
            { audioSettings.MaxDistance = maxDistanceBinding.Value; changed = true; }

            if (!Mathf.Approximately(audioSettings.Spread, spreadBinding.Value))
            { audioSettings.Spread = spreadBinding.Value; changed = true; }

            if (changed)
                audioSettings.IsDirty = true;
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
        private void SyncPositions(Entity entity, in CharacterTransform characterTransform, ref ProximityAudioSourceComponent proximityAudio)
        {
            if (proximityAudio.AudioSourceTransform == null)
            {
                entitiesToCleanUp.Add(entity);
                return;
            }

            proximityAudio.AudioSourceTransform.position = characterTransform.Position;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySettings(ref ProximityAudioSourceComponent proximityAudio)
        {
            if (proximityAudio.AudioSource != null)
                audioSettings.ApplyTo(proximityAudio.AudioSource);
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
