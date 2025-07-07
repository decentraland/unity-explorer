using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that handles Unity's OnAudioFilterRead callback and dispatches audio to LiveKit.
    ///     Focused solely on audio thread management and LiveKit buffering.
    ///     Audio processing is delegated to VoiceChatAudioProcessingManager.
    /// </summary>
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private const int DEFAULT_LIVEKIT_CHANNELS = 1;
        private const int LIVEKIT_FRAME_SIZE = 480; // 10ms at 48kHz

        private VoiceChatAudioProcessingManager processingManager;
        private bool isFilterActive = true;
        private int outputSampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;

        private readonly List<float> liveKitBuffer = new(LIVEKIT_FRAME_SIZE * 2);
        private float[] tempBuffer;
        private float[] processingBuffer;

        private VoiceChatConfiguration voiceChatConfiguration;
        private VoiceChatCombinedStreamsAudioSource combinedAudioSource;

        // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => processingManager != null && voiceChatConfiguration != null;

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

            if (processingBuffer != null)
                Array.Clear(processingBuffer, 0, processingBuffer.Length);

            outputSampleRate = AudioSettings.outputSampleRate;
        }

        private void OnDestroy()
        {
            AudioRead = null!;
            processingManager?.Dispose();
            processingManager = null;
            tempBuffer = null;
            processingBuffer = null;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isFilterActive || data == null || data.Length == 0)
                return;

            int samplesPerChannel = data.Length / channels;

            EnsureBuffers(samplesPerChannel);

            int processedSamples = processingManager.ProcessAudio(data, channels, outputSampleRate, samplesPerChannel, processingBuffer.AsSpan());

            for (int i = 0; i < processedSamples; i++)
                liveKitBuffer.Add(processingBuffer[i]);

            SendCompleteFramesToLiveKit();
        }

        private void EnsureBuffers(int samplesPerChannel)
        {
            if (tempBuffer == null || tempBuffer.Length < samplesPerChannel * 2)
                tempBuffer = new float[Mathf.Max(samplesPerChannel * 2, LIVEKIT_FRAME_SIZE * 2)];

            var maxProcessedSamples = VoiceChatAudioFormatConverter.CalculateLiveKitBufferSize(samplesPerChannel, outputSampleRate);
            if (processingBuffer == null || processingBuffer.Length < maxProcessedSamples)
                processingBuffer = new float[Mathf.Max(maxProcessedSamples, LIVEKIT_FRAME_SIZE * 2)];
        }

        private void SendCompleteFramesToLiveKit()
        {
            while (liveKitBuffer.Count >= LIVEKIT_FRAME_SIZE)
            {
                if (tempBuffer == null || tempBuffer.Length < LIVEKIT_FRAME_SIZE)
                    tempBuffer = new float[LIVEKIT_FRAME_SIZE];

                liveKitBuffer.CopyTo(0, tempBuffer, 0, LIVEKIT_FRAME_SIZE);

                AudioRead?.Invoke(tempBuffer.AsSpan(0, LIVEKIT_FRAME_SIZE), DEFAULT_LIVEKIT_CHANNELS, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);

                liveKitBuffer.RemoveRange(0, LIVEKIT_FRAME_SIZE);
            }
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration, VoiceChatCombinedStreamsAudioSource combinedAudioSource = null)
        {
            voiceChatConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.combinedAudioSource = combinedAudioSource;

            var audioProcessor = new OptimizedVoiceChatAudioProcessor(configuration);
            processingManager = new VoiceChatAudioProcessingManager(audioProcessor, configuration);

            VoiceChatAudioEchoCancellation.Initialize(configuration);
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;

            processingManager?.Reset();

            VoiceChatAudioEchoCancellation.Reset();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (processingBuffer != null)
                Array.Clear(processingBuffer, 0, processingBuffer.Length);

            liveKitBuffer.Clear();
        }

        public void Reset()
        {
            liveKitBuffer.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (processingBuffer != null)
                Array.Clear(processingBuffer, 0, processingBuffer.Length);

            processingManager?.Reset();
        }
    }
}
