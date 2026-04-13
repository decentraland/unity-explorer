using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    /// Applies <see cref="VoiceChatConfiguration"/> LiveKit spatial settings
    /// to every <see cref="NearbyAudioSourceComponent"/> each frame.
    /// Debug bindings are two-way: widget changes update config, external config changes update widget.
    /// Runs in <see cref="InitializationSystemGroup"/> so values are up-to-date
    /// before <see cref="NearbyAudioPositionSystem"/> processes positions.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class NearbyAudioDebugSystem : BaseUnityLoopSystem
    {
        private readonly VoiceChatConfiguration configuration;
        private readonly ElementBinding<bool> spatializeBinding;
        private readonly ElementBinding<bool> smoothPanningBinding;
        private readonly ElementBinding<float> ildBinding;

        internal NearbyAudioDebugSystem(World world, VoiceChatConfiguration configuration,
            IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.configuration = configuration;

            spatializeBinding = new ElementBinding<bool>(configuration.nearbySpatialize,
                evt => { configuration.nearbySpatialize = evt.newValue; });

            smoothPanningBinding = new ElementBinding<bool>(configuration.nearbySmoothPanning,
                evt => { configuration.nearbySmoothPanning = evt.newValue; });

            ildBinding = new ElementBinding<float>(configuration.nearbyIldStrength,
                evt => { configuration.nearbyIldStrength = evt.newValue; });

            debugBuilder.TryAddWidget("Voice Chat - Nearby")
                       ?.AddControl(new DebugConstLabelDef("Spatialize"), new DebugToggleDef(spatializeBinding))
                        .AddControl(new DebugConstLabelDef("Smooth Panning"), new DebugToggleDef(smoothPanningBinding))
                        .AddFloatSliderField("ILD Strength", ildBinding, 0f, 1f);
        }

        protected override void Update(float t)
        {
            bool changed = spatializeBinding.Value != configuration.nearbySpatialize
                        || smoothPanningBinding.Value != configuration.nearbySmoothPanning
                        || !Mathf.Approximately(ildBinding.Value, configuration.nearbyIldStrength);

            if (!changed)
                return;

            spatializeBinding.Value = configuration.nearbySpatialize;
            smoothPanningBinding.Value = configuration.nearbySmoothPanning;
            ildBinding.Value = configuration.nearbyIldStrength;

            ApplySettingsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySettings(ref NearbyAudioSourceComponent nearbyAudio)
        {
            configuration.ApplyLivekitSpatialSettings(nearbyAudio.LivekitAudioSource);
        }
    }
}
