using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.VoiceChat.Proximity.Systems
{
    /// <summary>
    /// Applies <see cref="VoiceChatConfiguration"/> LiveKit spatial settings
    /// to every <see cref="ProximityAudioSourceComponent"/> each frame.
    /// Debug bindings are two-way: widget changes update config, external config changes update widget.
    /// Runs in <see cref="InitializationSystemGroup"/> so values are up-to-date
    /// before <see cref="ProximityAudioPositionSystem"/> processes positions.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProximityAudioDebugSystem : BaseUnityLoopSystem
    {
        private readonly VoiceChatConfiguration configuration;
        private readonly ElementBinding<bool> spatializeBinding;
        private readonly ElementBinding<bool> smoothPanningBinding;
        private readonly ElementBinding<float> ildBinding;

        internal ProximityAudioDebugSystem(World world, VoiceChatConfiguration configuration,
            IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.configuration = configuration;

            spatializeBinding = new ElementBinding<bool>(configuration.proximitySpatialize,
                evt => { configuration.proximitySpatialize = evt.newValue; });

            smoothPanningBinding = new ElementBinding<bool>(configuration.proximitySmoothPanning,
                evt => { configuration.proximitySmoothPanning = evt.newValue; });

            ildBinding = new ElementBinding<float>(configuration.proximityIldStrength,
                evt => { configuration.proximityIldStrength = evt.newValue; });

            debugBuilder.TryAddWidget("Voice Chat - Proximity")
                       ?.AddControl(new DebugConstLabelDef("Spatialize"), new DebugToggleDef(spatializeBinding))
                        .AddControl(new DebugConstLabelDef("Smooth Panning"), new DebugToggleDef(smoothPanningBinding))
                        .AddFloatSliderField("ILD Strength", ildBinding, 0f, 1f);
        }

        protected override void Update(float t)
        {
            spatializeBinding.Value = configuration.proximitySpatialize;
            smoothPanningBinding.Value = configuration.proximitySmoothPanning;
            ildBinding.Value = configuration.proximityIldStrength;

            ApplySettingsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySettings(ref ProximityAudioSourceComponent proximityAudio)
        {
            configuration.ApplyLivekitSpatialSettings(proximityAudio.LivekitAudioSource);
        }
    }
}
