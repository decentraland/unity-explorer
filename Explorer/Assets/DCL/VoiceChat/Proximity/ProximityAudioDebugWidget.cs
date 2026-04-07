using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Registers a "Proximity Audio" debug widget with runtime sliders
    /// that write directly to <see cref="VoiceChatConfiguration"/> via
    /// <see cref="ElementBinding{T}.OnValueChanged"/> callbacks.
    /// </summary>
    public static class ProximityAudioDebugWidget
    {
        public static void Setup(IDebugContainerBuilder debugBuilder, VoiceChatConfiguration configuration)
        {
            var spreadBinding = new ElementBinding<float>(0f,
                evt => { configuration.ProximitySpread = evt.newValue; });

            debugBuilder.TryAddWidget("Proximity Audio")
                       ?.AddFloatSliderField("Spread", spreadBinding, 0f, 360f);
        }
    }
}
