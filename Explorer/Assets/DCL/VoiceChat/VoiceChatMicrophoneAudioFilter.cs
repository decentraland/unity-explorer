using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using LiveKit.Internal;
using LiveKit.Rooms.Streaming.Audio;
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
        private int cachedSampleRate; // Unity's output sample rate
        private readonly bool isProcessingEnabled = true; //Used for macOS to disable processing if exceptions occur, cannot be readonly
        private float[] silenceBuffer;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        private void Awake()
        {
            if (voiceChatConfiguration != null)
            {
                audioProcessor = new VoiceChatAudioProcessor(voiceChatConfiguration);
            }
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
            
            // Clear all AudioRead subscribers to prevent duplicate streams when switching microphones
            AudioRead = null;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "AudioFilter disabled - cleared AudioRead subscribers");
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

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // On macOS, periodically log buffer information to detect sample rate mismatches
            // Only log occasionally to avoid spam
            if (Time.frameCount % 480 == 0) // Log roughly every 10 seconds at 48fps
            {
                float bufferDurationMs = (float)data.Length / channels / cachedSampleRate * 1000f;
                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"macOS Audio Buffer Debug - BufferSize: {data.Length}, Channels: {channels}, " +
                    $"MonoSamples: {data.Length / channels}, Duration: {bufferDurationMs:F1}ms, " +
                    $"CachedSampleRate: {cachedSampleRate}Hz");
            }
#endif

            // Always convert to mono first, regardless of processing state
            float[] monoData = ConvertToMono(data, channels);

            // Note: No resampling needed here - Unity already provides audio at cachedSampleRate
            // The data parameter already contains audio at Unity's output sample rate
            
            if (isProcessingEnabled && audioProcessor != null && voiceChatConfiguration != null && monoData.Length > 0)
            {
                try
                {
                    // Process the mono audio at Unity's sample rate
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

            // Send mono audio when processing is disabled or failed
            AudioRead?.Invoke(monoData, 1, cachedSampleRate);
        }

        // Event is called from the Unity audio thread - LiveKit compatibility
        public event IAudioFilter.OnAudioDelegate AudioRead
        {
            add
            {
                field += value;
                ReportHub.Log(ReportCategory.VOICE_CHAT, 
                    $"AudioRead subscriber added - Total subscribers: {field?.GetInvocationList().Length ?? 0}");
            }
            remove
            {
                field -= value;
                ReportHub.Log(ReportCategory.VOICE_CHAT, 
                    $"AudioRead subscriber removed - Total subscribers: {field?.GetInvocationList().Length ?? 0}");
            }
        }

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // Always use Unity's output sample rate as target
            cachedSampleRate = AudioSettings.outputSampleRate;

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Audio configuration changed - DeviceChanged: {deviceWasChanged}, " +
                $"Unity OutputSampleRate: {cachedSampleRate}Hz");
                
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // On macOS, log additional audio system information for debugging
            var config = AudioSettings.GetConfiguration();
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"macOS Audio Debug - Unity Config SampleRate: {config.sampleRate}Hz, " +
                $"DSPBufferSize: {config.dspBufferSize}, OutputSampleRate: {AudioSettings.outputSampleRate}Hz");
#endif
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
            audioProcessor = new VoiceChatAudioProcessor(configuration);
        }

        public void ResetProcessor()
        {
            audioProcessor?.Reset();
            
            // Clear all AudioRead subscribers to prevent duplicate streams when resetting
            AudioRead = null;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "AudioProcessor reset - cleared AudioRead subscribers");
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

            for (var i = 0; i < length; i++) { silenceBuffer[i] = 0f; }

            return silenceBuffer;
        }
    }
}
