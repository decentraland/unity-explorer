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
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private VoiceChatAudioProcessor audioProcessor;
        private int cachedSampleRate;
        private bool isProcessingEnabled = true; //Used for macOS to disable processing if exceptions occur, cannot be readonly

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        private void Awake()
        {
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
            audioProcessor = null;
            tempBuffer = null;
        }

        /// <summary>
        ///     Unity's audio filter callback - processes audio in real-time
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
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
                Array.Copy(data, tempBuffer, data.Length);

                try
                {
                    // Process audio and convert to mono for voice chat transmission
                    float[] monoData = ProcessAudioToMono(tempBuffer, channels, cachedSampleRate);

                    // Send mono audio to LiveKit (more efficient for voice chat)
                    AudioRead?.Invoke(monoData, 1, cachedSampleRate);
                    return;
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

            // Fallback: send original data if processing failed or is disabled
            AudioRead?.Invoke(data, channels, cachedSampleRate);
        }

        // Event is called from the Unity audio thread - LiveKit compatibility
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            cachedSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
            audioProcessor = new VoiceChatAudioProcessor(configuration);
        }

        public void ResetProcessor()
        {
            audioProcessor?.Reset();
        }

        private float[] ProcessAudioToMono(float[] data, int channels, int sampleRate)
        {
            // Since we force mono at the AudioSource level, we should always get mono-like data
            // But we'll handle any edge cases where multi-channel data still comes through

            if (channels == 1)
            {
                // True mono audio - process directly and return
                audioProcessor.ProcessAudio(data, sampleRate);
                return data;
            }
            else
            {
                // Multi-channel input (shouldn't happen with our forced mono setup, but safety fallback)
                // Convert to mono by taking only the first channel (most efficient for forced mono sources)
                int samplesPerChannel = data.Length / channels;
                float[] monoData = new float[samplesPerChannel];

                // Extract first channel only (more efficient than averaging since source is forced mono)
                for (var sampleIndex = 0; sampleIndex < samplesPerChannel; sampleIndex++)
                {
                    monoData[sampleIndex] = data[sampleIndex * channels];
                }

                audioProcessor.ProcessAudio(monoData, sampleRate);
                return monoData;
            }
        }
    }
}
