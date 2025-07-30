using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using System;
using System.Collections.Generic;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that applies resampling and volume increase to microphone input
    ///     using Unity's OnAudioFilterRead callback. Compatible with LiveKit's AudioFilter interface.
    ///     Processes audio directly on the Unity audio thread for minimal latency.
    /// </summary>
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private const int DEFAULT_LIVEKIT_CHANNELS = 2;
        private const int DEFAULT_BUFFER_SIZE = 8192;
        private const int LIVEKIT_FRAME_SIZE = 240; // 5ms at 48kHz (stereo samples)

        private readonly List<float> liveKitBuffer = new (LIVEKIT_FRAME_SIZE * 2); // Buffer for stereo data

        private bool isFilterActive = true;
        private int outputSampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;

        private float[] tempBuffer;
        private float[] resampleBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        public void Reset()
        {
            liveKitBuffer.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);
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
        ///     Processes audio directly on the audio thread for minimal latency
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isFilterActive || data == null || voiceChatConfiguration == null)
                return;

            int samplesPerChannel = data.Length / channels;
            Span<float> processedData = ProcessAudioDirectly(data, samplesPerChannel);

            // Buffer the audio for LiveKit
            // For stereo data, we buffer all samples (not just samplesPerChannel)
            for (var i = 0; i < processedData.Length; i++)
                liveKitBuffer.Add(processedData[i]);

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
        ///     Processes audio data directly on the audio thread
        /// </summary>
        private Span<float> ProcessAudioDirectly(float[] data, int samplesPerChannel)
        {
            // Apply volume directly
            float volumeMultiplier = voiceChatConfiguration.MicrophoneVolume;
            for (var i = 0; i < data.Length; i++)
            {
                data[i] *= volumeMultiplier;
            }

            // Handle resampling if needed
            if (outputSampleRate != VoiceChatConstants.LIVEKIT_SAMPLE_RATE)
            {
                return ResampleAudio(data, samplesPerChannel);
            }

            return data.AsSpan();
        }

        /// <summary>
        ///     Resamples audio to LiveKit sample rate
        /// </summary>
        private Span<float> ResampleAudio(float[] data, int samplesPerChannel)
        {
            var targetSamplesPerChannel = (int)((float)samplesPerChannel * VoiceChatConstants.LIVEKIT_SAMPLE_RATE / outputSampleRate);
            int targetTotalSamples = targetSamplesPerChannel * 2;

            // Ensure resample buffer is large enough
            if (resampleBuffer == null || resampleBuffer.Length < targetTotalSamples)
                resampleBuffer = new float[Mathf.Max(targetTotalSamples, DEFAULT_BUFFER_SIZE)];

            Span<float> resampledSpan = resampleBuffer.AsSpan(0, targetTotalSamples);

            VoiceChatMicrophoneAudioHelpers.ResampleStereo(
                data.AsSpan(),
                outputSampleRate,
                resampledSpan,
                VoiceChatConstants.LIVEKIT_SAMPLE_RATE);

            return resampledSpan;
        }

        /// <summary>
        ///     Event called from the Unity audio thread when audio data is available
        /// </summary>
        // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);

            liveKitBuffer.Clear();
        }
    }
}
