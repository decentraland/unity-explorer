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

        private bool prevSpatialize;
        private bool prevSmoothPanning;
        private float prevIldStrength;

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

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.NEARBY_VOICE_CHAT)
                       ?.AddControl(new DebugConstLabelDef("Spatialize"), new DebugToggleDef(spatializeBinding))
                        .AddControl(new DebugConstLabelDef("Smooth Panning"), new DebugToggleDef(smoothPanningBinding))
                        .AddFloatSliderField("ILD Strength", ildBinding, 0f, 1f);

            prevSpatialize = configuration.nearbySpatialize;
            prevSmoothPanning = configuration.nearbySmoothPanning;
            prevIldStrength = configuration.nearbyIldStrength;
        }

        protected override void Update(float t)
        {
            bool changed = prevSpatialize != configuration.nearbySpatialize
                        || prevSmoothPanning != configuration.nearbySmoothPanning
                        || !Mathf.Approximately(prevIldStrength, configuration.nearbyIldStrength);

            if (!changed)
                return;

            prevSpatialize = configuration.nearbySpatialize;
            prevSmoothPanning = configuration.nearbySmoothPanning;
            prevIldStrength = configuration.nearbyIldStrength;

            spatializeBinding.Value = configuration.nearbySpatialize;
            smoothPanningBinding.Value = configuration.nearbySmoothPanning;
            ildBinding.Value = configuration.nearbyIldStrength;

            ApplySettingsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySettings(ref NearbyAudioSourceComponent nearbyAudio)
        {
            nearbyAudio.LivekitAudioSource.ApplySpatialSettings(configuration);
        }
    }
}
