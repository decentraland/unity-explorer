using UnityEngine;
using LiveKit;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that applies real-time noise reduction and audio processing
    ///     to the microphone input using Unity's OnAudioFilterRead callback.
    ///     Compatible with LiveKit's AudioFilter interface.
    ///     Processing and resampling are done in a separate thread to avoid audio dropouts.
    /// </summary>
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private const int DEFAULT_LIVEKIT_CHANNELS = 1;
        private const int DEFAULT_BUFFER_SIZE = 8192;
        private const int LIVEKIT_FRAME_SIZE = 480; // 10ms at 48kHz

        private bool isFilterActive = true;
        private readonly List<float> liveKitBuffer = new (LIVEKIT_FRAME_SIZE * 2);
        private int outputSampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
        private float[] resampleBuffer;

        private float[] tempBuffer;

        private struct ProcessedAudioData
        {
            public float[] ProcessedData;
            public int SamplesPerChannel;
        }

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;

            liveKitBuffer.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);

            outputSampleRate = AudioSettings.outputSampleRate;
        }

        private void OnDestroy()
        {
            AudioRead = null!;
            tempBuffer = null;
            resampleBuffer = null;
        }

        /// <summary>
        ///     Unity's audio filter callback
        ///     Handles buffering and sending to LiveKit, processing is done in separate thread
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isFilterActive || data == null || data.Length == 0)
                return;

            int samplesPerChannel = data.Length / channels;

            // Ensure we have a temp buffer for format conversion
            if (tempBuffer == null || tempBuffer.Length < samplesPerChannel * 2)
                tempBuffer = new float[Mathf.Max(samplesPerChannel * 2, DEFAULT_BUFFER_SIZE)];

            Span<float> monoSpan = tempBuffer.AsSpan(0, samplesPerChannel);

            // Convert to mono if needed
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

            // Convert to LiveKit format (resample if needed)
            var processedData = ConvertToLiveKitFormat(monoSpan, samplesPerChannel, outputSampleRate, tempBuffer);

            // Buffer the audio for LiveKit
            for (var i = 0; i < processedData.SamplesPerChannel; i++)
                liveKitBuffer.Add(processedData.ProcessedData[i]);

            // Send complete frames to LiveKit
            while (liveKitBuffer.Count >= LIVEKIT_FRAME_SIZE)
            {
                if (tempBuffer == null || tempBuffer.Length < LIVEKIT_FRAME_SIZE)
                    tempBuffer = new float[LIVEKIT_FRAME_SIZE];

                liveKitBuffer.CopyTo(0, tempBuffer, 0, LIVEKIT_FRAME_SIZE);
                AudioRead?.Invoke(tempBuffer.AsSpan(0, LIVEKIT_FRAME_SIZE), DEFAULT_LIVEKIT_CHANNELS, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);
                liveKitBuffer.RemoveRange(0, LIVEKIT_FRAME_SIZE);
            }
        }

        /// <summary>
        ///     Event called from the Unity audio thread when audio data is available
        /// </summary>
        // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => true;

        private ProcessedAudioData ConvertToLiveKitFormat(Span<float> monoSpan, int samplesPerChannel, int sampleRate, float[] tempBuffer)
        {
            if (outputSampleRate != VoiceChatConstants.LIVEKIT_SAMPLE_RATE)
            {
                var targetSamplesPerChannel = (int)((float)samplesPerChannel * VoiceChatConstants.LIVEKIT_SAMPLE_RATE / outputSampleRate);
                Span<float> resampledSpan = tempBuffer.AsSpan(samplesPerChannel, targetSamplesPerChannel);
                VoiceChatAudioResampler.ResampleCubic(monoSpan.Slice(0, samplesPerChannel), outputSampleRate, resampledSpan, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);

                var result = new float[targetSamplesPerChannel];
                resampledSpan.CopyTo(result);

                return new ProcessedAudioData
                {
                    ProcessedData = result,
                    SamplesPerChannel = targetSamplesPerChannel
                };
            }

            var monoResult = new float[samplesPerChannel];
            monoSpan.Slice(0, samplesPerChannel).CopyTo(monoResult);

            return new ProcessedAudioData
            {
                ProcessedData = monoResult,
                SamplesPerChannel = samplesPerChannel
            };
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;
            VoiceChatAudioEchoCancellation.Reset();
            VoiceChatWebRTCAEC.Reset();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);
        }

        public void Reset()
        {
            liveKitBuffer.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);
        }
    }
}
