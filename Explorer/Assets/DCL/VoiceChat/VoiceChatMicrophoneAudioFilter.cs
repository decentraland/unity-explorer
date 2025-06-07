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
        private int cachedSampleRate;
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
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = true;
            }

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
                Array.Copy(data, tempBuffer, data.Length);

                try
                {
                    if (channels == 2)
                    {
                        // Stereo audio - process each channel separately (LiveKit standard)
                        ProcessStereoAudio(tempBuffer, cachedSampleRate);
                    }
                    else if (channels == 1)
                    {
                        // Mono audio - process directly
                        audioProcessor.ProcessAudio(tempBuffer, cachedSampleRate);
                    }
                    else
                    {
                        // Multi-channel audio - process as interleaved
                        ProcessMultiChannelAudio(tempBuffer, channels, cachedSampleRate);
                    }

                    // Copy processed data back
                    Array.Copy(tempBuffer, data, data.Length);
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

            // Always invoke the AudioRead event for LiveKit compatibility
            // This sends the processed audio data to LiveKit
            // Send even empty buffers to maintain audio stream continuity
            AudioRead?.Invoke(data, channels, cachedSampleRate);
        }

        // Event is called from the Unity audio thread - LiveKit compatibility
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => audioSource != null && audioProcessor != null && voiceChatConfiguration != null;

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            cachedSampleRate = AudioSettings.outputSampleRate;
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
            isProcessingEnabled = active;
            
            if (!active)
            {
                audioProcessor?.Reset();
                if (tempBuffer != null)
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);
            }
        }

        private void ProcessStereoAudio(float[] data, int sampleRate)
        {
            // Optimized stereo processing for LiveKit's 2-channel 48kHz format
            // Process left and right channels separately
            for (var i = 0; i < data.Length; i += 2)
            {
                float[] leftSample = { data[i] };
                float[] rightSample = { data[i + 1] };

                audioProcessor.ProcessAudio(leftSample, sampleRate);
                audioProcessor.ProcessAudio(rightSample, sampleRate);

                data[i] = leftSample[0]; // Left channel
                data[i + 1] = rightSample[0]; // Right channel
            }
        }

        private void ProcessMultiChannelAudio(float[] data, int channels, int sampleRate)
        {
            // For multi-channel audio, process each channel separately
            int samplesPerChannel = data.Length / channels;

            for (var channel = 0; channel < channels; channel++)
            {
                var channelData = new float[samplesPerChannel];

                for (var i = 0; i < samplesPerChannel; i++)
                    channelData[i] = data[(i * channels) + channel];

                audioProcessor.ProcessAudio(channelData, sampleRate);

                for (var i = 0; i < samplesPerChannel; i++)
                    data[(i * channels) + channel] = channelData[i];
            }
        }
    }
}
