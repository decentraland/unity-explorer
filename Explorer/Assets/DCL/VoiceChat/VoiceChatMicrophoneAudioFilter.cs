using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using LiveKit.Internal;
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
        private int microphoneSampleRate; // Microphone's actual sample rate
        private readonly bool isProcessingEnabled = true; //Used for macOS to disable processing if exceptions occur, cannot be readonly
        private float[] silenceBuffer;
        private AudioResampler.ThreadSafe resampler;

        private float[] tempBuffer;
        private float[] resampledBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        private void Awake()
        {
            if (voiceChatConfiguration != null) 
            {
                audioProcessor = new VoiceChatAudioProcessor(voiceChatConfiguration);
            }
            resampler = new AudioResampler.ThreadSafe();
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
            resampler?.Dispose();
            tempBuffer = null;
            resampledBuffer = null;
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
            
            // Resample from microphone rate to Unity's output rate if needed
            float[] processedData = ResampleToUnityRate(monoData);

            if (isProcessingEnabled && audioProcessor != null && voiceChatConfiguration != null && processedData.Length > 0)
            {
                try
                {
                    // Process the mono audio at Unity's sample rate
                    audioProcessor.ProcessAudio(processedData, cachedSampleRate);
                    AudioRead?.Invoke(processedData, 1, cachedSampleRate);
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

            // Send resampled mono audio when processing is disabled or failed
            AudioRead?.Invoke(processedData, 1, cachedSampleRate);
        }

        // Event is called from the Unity audio thread - LiveKit compatibility
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // Always use Unity's output sample rate as target
            cachedSampleRate = AudioSettings.outputSampleRate;
            
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Audio configuration changed - DeviceChanged: {deviceWasChanged}, " +
                $"Unity OutputSampleRate: {cachedSampleRate}Hz, Microphone SampleRate: {microphoneSampleRate}Hz");
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
        ///     Updates the cached microphone sample rate
        ///     Call this when the microphone is initialized or changed
        /// </summary>
        public void UpdateSampleRate(int microphoneSampleRate)
        {
            if (microphoneSampleRate != this.microphoneSampleRate)
            {
                this.microphoneSampleRate = microphoneSampleRate;

                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"Updated microphone sample rate: {microphoneSampleRate}Hz, Unity target rate: {cachedSampleRate}Hz");
            }
        }

        private float[] ResampleToUnityRate(float[] monoData)
        {
            // If sample rates match, no resampling needed
            if (microphoneSampleRate == cachedSampleRate || microphoneSampleRate <= 0 || cachedSampleRate <= 0)
            {
                return monoData;
            }

            try
            {
                // Convert float array to AudioFrame for resampling
                var audioFrame = ConvertToAudioFrame(monoData, microphoneSampleRate);
                
                // Resample from microphone rate TO Unity's configured rate
                using var resampledFrame = resampler.RemixAndResample(
                    audioFrame, 
                    1, // mono
                    (uint)cachedSampleRate
                );
                
                // Convert back to float array
                return ConvertFromAudioFrame(resampledFrame);
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, 
                    $"Resampling failed from {microphoneSampleRate}Hz to {cachedSampleRate}Hz: {ex.Message}. Using original data.");
                return monoData;
            }
        }

        private OwnedAudioFrame ConvertToAudioFrame(float[] data, int sampleRate)
        {
            // Convert float samples to short for AudioFrame
            short[] shortData = new short[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                shortData[i] = (short)(data[i] * short.MaxValue);
            }
            
            return new OwnedAudioFrame((uint)sampleRate, 1, shortData);
        }

        private float[] ConvertFromAudioFrame(OwnedAudioFrame frame)
        {
            var frameSpan = frame.AsSpan();
            float[] result = new float[frameSpan.Length];
            
            for (int i = 0; i < frameSpan.Length; i++)
            {
                result[i] = frameSpan[i] / (float)short.MaxValue;
            }
            
            return result;
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
