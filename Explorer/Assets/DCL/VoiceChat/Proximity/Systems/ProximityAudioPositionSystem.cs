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
    /// <see cref="CharacterTransform"/> each frame, and applies spatial audio settings.
    /// <see cref="VoiceChatConfiguration"/> is the single source of truth.
    /// A dedicated "Proximity Audio" debug widget provides runtime sliders that write
    /// directly to the SO via <see cref="ElementBinding{T}.OnValueChanged"/> callbacks.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    public partial class ProximityAudioPositionSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ProximityConfigHolder configHolder;
        private readonly List<Entity> entitiesToCleanUp = new ();

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

            var maxDistanceBinding = new ElementBinding<float>(50f,
                evt => { if (configHolder.Config != null) configHolder.Config.ProximityMaxDistance = evt.newValue; });

            var spreadBinding = new ElementBinding<float>(0f,
                evt => { if (configHolder.Config != null) configHolder.Config.ProximitySpread = evt.newValue; });

            debugBuilder.TryAddWidget("Proximity Audio")
                       ?.AddFloatSliderField("Spatial Blend", spatialBlendBinding, 0f, 1f)
                        .AddFloatSliderField("Doppler Level", dopplerBinding, 0f, 5f)
                        .AddFloatSliderField("Min Distance", minDistanceBinding, 0f, 100f)
                        .AddFloatSliderField("Max Distance", maxDistanceBinding, 1f, 500f)
                        .AddFloatSliderField("Spread", spreadBinding, 0f, 360f);
        }

        protected override void Update(float t)
        {
            if (configHolder.Config == null) return;

            AssignPendingSources();
            SyncPositionsQuery(World);
            ApplySettingsQuery(World);
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
