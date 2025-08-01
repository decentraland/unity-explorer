using UnityEngine;
using LiveKit;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private bool isFilterActive = true;
        private VoiceChatConfiguration voiceChatConfiguration;

        public void Reset()
        {
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnDestroy()
        {
            AudioRead = null!;
        }

        /// <summary>
        ///     Unity's audio filter callback
        ///     Passes audio directly on the audio thread for minimal latency
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isFilterActive || data == null || voiceChatConfiguration == null)
                return;

            AudioRead?.Invoke(data.AsSpan(), channels, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);
        }

        /// <summary>
        ///     Event called from the Unity audio thread when audio data is available
        /// </summary>
        // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;
        }
    }
}
