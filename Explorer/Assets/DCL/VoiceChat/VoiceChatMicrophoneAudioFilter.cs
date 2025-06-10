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
        private const int DEFAULT_BUFFER_SIZE = 8192;

        private VoiceChatAudioProcessor audioProcessor;
        private bool isFilterActive = true;
        private int outputSampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
        private float[] resampleBuffer;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;
        private bool isProcessingEnabled => voiceChatConfiguration != null && voiceChatConfiguration.EnableAudioProcessing;

        private void Awake()
        {
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
            resampleBuffer = null;
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
            AudioRead?.Invoke(data.AsSpan(), 2, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);
        }

        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void ProcessAudioData(Span<float> data, int channels, int sampleRate)
        {
            int samplesPerChannel = data.Length / channels;
            Span<float> monoSpan = tempBuffer.AsSpan(0, samplesPerChannel);

            if (channels > 1)
            {
                for (var i = 0; i < samplesPerChannel; i++)
                {
                    var sum = 0f;
                    for (var ch = 0; ch < channels; ch++)
                        sum += data[(i * channels) + ch];
                    monoSpan[i] = sum / channels;
                }
            }
            else
            {
                data.CopyTo(monoSpan);
            }

            audioProcessor.ProcessAudio(monoSpan, sampleRate);

            if (outputSampleRate != VoiceChatConstants.LIVEKIT_SAMPLE_RATE)
            {
                int targetSamplesPerChannel = (int)((float)samplesPerChannel * VoiceChatConstants.LIVEKIT_SAMPLE_RATE / outputSampleRate);
                Span<float> resampledSpan = tempBuffer.AsSpan(samplesPerChannel, targetSamplesPerChannel);

                // Resample working backwards to avoid overwriting data
                for (int i = targetSamplesPerChannel - 1; i >= 0; i--)
                {
                    float sourceIndex = (float)i * outputSampleRate / VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
                    int sourceIndexFloor = Mathf.FloorToInt(sourceIndex);
                    int sourceIndexCeil = Mathf.Min(sourceIndexFloor + 1, samplesPerChannel - 1);
                    float fraction = sourceIndex - sourceIndexFloor;

                    float sample1 = monoSpan[sourceIndexFloor];
                    float sample2 = monoSpan[sourceIndexCeil];
                    resampledSpan[i] = Mathf.Lerp(sample1, sample2, fraction);
                }

                monoSpan = resampledSpan;
                samplesPerChannel = targetSamplesPerChannel;
            }

            // Ensure 2-channel output with processed audio in left channel only
            for (var i = 0; i < samplesPerChannel; i++)
            {
                data[i * 2] = monoSpan[i];     // Left channel: processed audio
                data[i * 2 + 1] = 0f;         // Right channel: silence
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

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);
        }
    }
}
