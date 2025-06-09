using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that applies real-time noise reduction and audio processing
    ///     to the microphone input using Unity's OnAudioFilterRead callback.
    ///     Compatible with LiveKit's AudioFilter interface.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private const int DEFAULT_BUFFER_SIZE = 8192;

        private VoiceChatAudioProcessor audioProcessor;
        private AudioSource audioSource;
        private bool isFilterActive = true;
        private bool isProcessingEnabled => voiceChatConfiguration != null && voiceChatConfiguration.EnableAudioProcessing;
        private int outputSampleRate;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();

            if (voiceChatConfiguration != null) audioProcessor = new VoiceChatAudioProcessor(voiceChatConfiguration);
        }

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnDestroy()
        {
            AudioRead = null!;

            audioProcessor?.Dispose();
            audioProcessor = null;
            tempBuffer = null;
        }

        /// <summary>
        ///     Unity's audio filter callback - processes audio in real-time
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isFilterActive)
                return;

            if (data == null)
                return;

            if (isProcessingEnabled && audioProcessor != null && data.Length > 0)
            {
                if (tempBuffer == null || tempBuffer.Length < data.Length)
                    tempBuffer = new float[Mathf.Max(data.Length, DEFAULT_BUFFER_SIZE)];

                try { ProcessAudioData(data.AsSpan(), channels, outputSampleRate); }
                catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error: {ex.Message}"); }
            }

            // This sends the processed audio data to LiveKit
            AudioRead?.Invoke(data.AsSpan(), channels, outputSampleRate);
        }

        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioSource != null && audioProcessor != null && voiceChatConfiguration != null;

        private void ProcessAudioData(Span<float> data, int channels, int sampleRate)
        {
            if (channels == 1)
            {
                // Mono audio - process directly in-place
                audioProcessor.ProcessAudio(data, sampleRate);
            }
            else
            {
                // Multi-channel audio - convert to mono, process, then send on left channel only
                ConvertToMonoProcessAndSendLeftChannel(data, channels, sampleRate);
            }
        }

        private void ConvertToMonoProcessAndSendLeftChannel(Span<float> data, int channels, int sampleRate)
        {
            int samplesPerChannel = data.Length / channels;

            Span<float> monoSpan = tempBuffer.AsSpan(0, samplesPerChannel);

            for (var i = 0; i < samplesPerChannel; i++)
            {
                var sum = 0f;

                for (var ch = 0; ch < channels; ch++) { sum += data[(i * channels) + ch]; }

                monoSpan[i] = sum / channels; // Average all channels
            }

            audioProcessor.ProcessAudio(monoSpan, sampleRate);

            for (var i = 0; i < samplesPerChannel; i++)
            {
                data[i * channels] = monoSpan[i]; // Processed mono on left channel

                // Zero out all other channels
                for (var ch = 1; ch < channels; ch++) { data[(i * channels) + ch] = 0f; }
            }
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
            audioProcessor?.Dispose();
            audioProcessor = new VoiceChatAudioProcessor(configuration);
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;
            audioProcessor?.Reset();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);
        }
    }
}
