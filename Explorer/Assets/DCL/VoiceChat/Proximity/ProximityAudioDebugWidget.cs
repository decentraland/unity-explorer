using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Registers a "Proximity Audio" debug widget with runtime sliders
    /// that write directly to <see cref="VoiceChatConfiguration"/> via
    /// <see cref="ElementBinding{T}.OnValueChanged"/> callbacks.
    /// </summary>
    public static class ProximityAudioDebugWidget
    {
        public static void Setup(IDebugContainerBuilder debugBuilder, ProximityConfigHolder configHolder)
        {
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
    }
}
