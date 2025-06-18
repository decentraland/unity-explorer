using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using System;
using DCL.VoiceChat;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that applies real-time noise reduction and audio processing
    ///     to the microphone input using Unity's OnAudioFilterRead callback.
    ///     Compatible with LiveKit's AudioFilter interface.
    /// </summary>
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private const int DEFAULT_LIVEKIT_CHANNELS = 1;
        private const int DEFAULT_BUFFER_SIZE = 8192;

        private IVoiceChatAudioProcessor audioProcessor;
        private bool isFilterActive = true;
        private int outputSampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
        private float[] resampleBuffer;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;
        private bool isProcessingEnabled => voiceChatConfiguration != null && voiceChatConfiguration.EnableAudioProcessing;

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

            int samplesPerChannel = data.Length / channels;
            Span<float> sendBuffer = data;

            if (isProcessingEnabled && audioProcessor != null && data.Length > 0)
            {
                if (tempBuffer == null || tempBuffer.Length < data.Length * 2)
                    tempBuffer = new float[Mathf.Max(data.Length * 2, DEFAULT_BUFFER_SIZE)];

                try { sendBuffer = ProcessAudioData(data.AsSpan(), channels, outputSampleRate, ref samplesPerChannel); }
                catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error: {ex.Message}"); }
            }

            // Send the actual processed buffer to LiveKit
            AudioRead?.Invoke(sendBuffer.Slice(0, samplesPerChannel), DEFAULT_LIVEKIT_CHANNELS, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);
        }

        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private Span<float> ProcessAudioData(Span<float> data, int channels, int sampleRate, ref int samplesPerChannel)
        {
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
                VoiceChatAudioResampler.ResampleCubic(monoSpan.Slice(0, samplesPerChannel), outputSampleRate, resampledSpan, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);
                samplesPerChannel = targetSamplesPerChannel;
                return resampledSpan;
            }

            return monoSpan;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
            audioProcessor = new OptimizedVoiceChatAudioProcessor(configuration);
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
