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
        private VoiceChatAudioProcessor audioProcessor;
        private AudioSource audioSource;
        private int outputSampleRate;
        private bool isProcessingEnabled = true;
        private bool isFilterActive = true;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();

            if (voiceChatConfiguration != null) audioProcessor = new VoiceChatAudioProcessor(voiceChatConfiguration);
        }

        private void Start()
        {
            // Pre-allocate buffer on macOS to avoid allocations in audio thread
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (tempBuffer == null)
                tempBuffer = new float[8192]; // Pre-allocate large buffer for macOS Core Audio compatibility
#endif
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

            if (isProcessingEnabled && audioProcessor != null && voiceChatConfiguration != null && data.Length > 0)
            {
                // On macOS, avoid allocations in audio thread due to Core Audio sensitivity
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                if (tempBuffer == null)
                {
                    // Pre-allocate a reasonably large buffer to avoid reallocations
                    tempBuffer = new float[8192];
                }

                if (tempBuffer.Length < data.Length)
                {
                    // If we absolutely must reallocate, do it but log a warning
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio buffer reallocation on macOS (from {tempBuffer.Length} to {data.Length}). This may cause audio glitches.");
                    tempBuffer = new float[Mathf.Max(data.Length, 8192)];
                }
#else
                if (tempBuffer == null || tempBuffer.Length != data.Length) { tempBuffer = new float[data.Length]; }
#endif

                try
                {
                    if (channels == 1)
                    {
                        // Mono audio - process directly
                        Array.Copy(data, tempBuffer, data.Length);
                        audioProcessor.ProcessAudio(tempBuffer, outputSampleRate);
                        Array.Copy(tempBuffer, data, data.Length);
                    }
                    else
                    {
                        // Multi-channel audio - convert to mono, process, send on left channel only
                        ConvertToMonoProcessAndSendSingleChannel(data, channels, outputSampleRate);
                    }
                }
                catch (Exception ex)
                {
                    // On macOS, Core Audio is very sensitive to exceptions in audio callbacks
                    // Disable processing to prevent further issues
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error on macOS: {ex.Message}. Disabling processing to prevent audio system instability.");
                    isProcessingEnabled = false;
#else
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error: {ex.Message}");
#endif
                }
            }

            // This sends the processed audio data to LiveKit
            AudioRead?.Invoke(data, channels, outputSampleRate);
        }

        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioSource != null && audioProcessor != null && voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
            audioProcessor?.Dispose();
            audioProcessor = new VoiceChatAudioProcessor(configuration);
            isProcessingEnabled = configuration.EnableAudioProcessing;
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;
            isProcessingEnabled = active && voiceChatConfiguration != null && voiceChatConfiguration.EnableAudioProcessing;

            if (!isProcessingEnabled)
            {
                audioProcessor?.Reset();
                if (tempBuffer != null)
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);
            }
        }

        private void ConvertToMonoProcessAndSendSingleChannel(float[] data, int channels, int sampleRate)
        {
            int samplesPerChannel = data.Length / channels;

            // Convert multi-channel to mono by averaging all channels
            for (int i = 0; i < samplesPerChannel; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += data[i * channels + ch];
                }
                tempBuffer[i] = sum / channels; // Average all channels
            }

            // Process the mono audio
            float[] monoBuffer = new float[samplesPerChannel];
            Array.Copy(tempBuffer, monoBuffer, samplesPerChannel);
            audioProcessor.ProcessAudio(monoBuffer, sampleRate);

            // Send processed audio on left channel only, silence other channels
            for (int i = 0; i < samplesPerChannel; i++)
            {
                data[i * channels] = monoBuffer[i];

                for (int ch = 1; ch < channels; ch++)
                {
                    data[i * channels + ch] = 0f;
                }
            }
        }
    }
}
