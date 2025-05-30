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
        private bool cachedEnabled = true; // Cache enabled state for audio thread access
        private int cachedSampleRate;
        private readonly bool isProcessingEnabled = true; //Used for macOS to disable processing if exceptions occur, cannot be readonly
        private float[] silenceBuffer;

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
            cachedEnabled = true;
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            cachedEnabled = false;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnDestroy()
        {
            AudioRead = null!;
            audioProcessor = null;
            tempBuffer = null;
            silenceBuffer = null;
        }

        /// <summary>
        ///     Unity's audio filter callback - processes audio in real-time
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null)
                return;

            if (!cachedEnabled)
            {
                float[] silenceBuffer = GetSilenceBuffer(data.Length / channels);
                AudioRead?.Invoke(silenceBuffer, 1, cachedSampleRate);
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Sending silence to LiveKit - AudioFilter disabled");
                return;
            }

            // Always convert to mono first, regardless of processing state
            float[] monoData = ConvertToMono(data, channels);

            if (isProcessingEnabled && audioProcessor != null && voiceChatConfiguration != null && monoData.Length > 0)
            {
                try
                {
                    // Process the mono audio
                    audioProcessor.ProcessAudio(monoData, cachedSampleRate);
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

            // Send raw mono audio when processing is disabled or failed
            AudioRead?.Invoke(monoData, 1, cachedSampleRate);
        }

        // Event is called from the Unity audio thread - LiveKit compatibility
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // Don't automatically change sample rate here - let the microphone handler set it explicitly
            // Only update if we don't have a valid sample rate yet
            if (cachedSampleRate <= 0)
            {
                cachedSampleRate = AudioSettings.outputSampleRate;

                ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                    $"Audio configuration changed but no microphone sample rate set - using Unity output rate: {cachedSampleRate}Hz");
            }
            else
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"Audio configuration changed - DeviceChanged: {deviceWasChanged}, " +
                    $"Current Microphone SampleRate: {cachedSampleRate}Hz, Unity OutputSampleRate: {AudioSettings.outputSampleRate}Hz");
            }
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

        /// <summary>
        ///     Updates the cached sample rate from the microphone AudioClip
        ///     Call this when the microphone is initialized or changed
        /// </summary>
        public void UpdateSampleRate(int microphoneSampleRate)
        {
            if (microphoneSampleRate != cachedSampleRate)
            {
                cachedSampleRate = microphoneSampleRate;

                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"Updated cached sample rate to microphone frequency: {cachedSampleRate}Hz");
            }
        }

        private float[] ConvertToMono(float[] data, int channels)
        {
            if (channels == 1)
            {
                // Already mono - return as-is
                return data;
            }

            // Multi-channel input - convert to mono by averaging all channels
            int samplesPerChannel = data.Length / channels;

            // Ensure we have a properly sized buffer
            if (tempBuffer == null || tempBuffer.Length < samplesPerChannel)
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                tempBuffer = new float[Mathf.Max(samplesPerChannel, 8192)];
#else
                tempBuffer = new float[samplesPerChannel];
#endif
            }

            // Average all channels - standard mono conversion
            for (var sampleIndex = 0; sampleIndex < samplesPerChannel; sampleIndex++)
            {
                var sum = 0f;

                for (var ch = 0; ch < channels; ch++) { sum += data[(sampleIndex * channels) + ch]; }

                tempBuffer[sampleIndex] = sum / channels;
            }

            return tempBuffer;
        }

        private float[] GetSilenceBuffer(int length)
        {
            if (silenceBuffer == null || silenceBuffer.Length < length) { silenceBuffer = new float[Mathf.Max(length, 1024)]; }

            // Always clear the buffer to ensure silence
            Array.Clear(silenceBuffer, 0, Mathf.Min(length, silenceBuffer.Length));
            return silenceBuffer;
        }
    }
}
