using DCL.Settings.Settings;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Custom AudioFilter that applies real-time noise reduction and audio processing
    /// to the microphone input using Unity's OnAudioFilterRead callback.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour
    {
        [SerializeField] private VoiceChatSettingsAsset voiceChatSettings;
        
        private VoiceChatAudioProcessor audioProcessor;
        private AudioSource audioSource;
        private bool isProcessingEnabled = true;
        
        // Performance optimization - reuse buffer
        private float[] tempBuffer;
        
        // Cache sample rate to avoid main thread calls in audio thread
        private int cachedSampleRate;
        
        public bool IsProcessingEnabled 
        { 
            get => isProcessingEnabled; 
            set => isProcessingEnabled = value; 
        }
        
        public bool IsNoiseGateOpen => audioProcessor?.IsGateOpen ?? false;
        public float CurrentGain => audioProcessor?.CurrentGain ?? 1f;
        
        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            
            // Use the microphone's fixed sample rate (48000) for consistency
            cachedSampleRate = 48000;
            
            if (voiceChatSettings != null)
            {
                audioProcessor = new VoiceChatAudioProcessor(voiceChatSettings);
            }
        }
        
        private void Start()
        {
            // Ensure the AudioSource is configured for microphone input
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = true;
            }
        }
        
        private void OnDestroy()
        {
            audioProcessor = null;
            tempBuffer = null;
        }
        
        /// <summary>
        /// Initialize the audio filter with settings
        /// </summary>
        public void Initialize(VoiceChatSettingsAsset settings)
        {
            voiceChatSettings = settings;
            audioProcessor = new VoiceChatAudioProcessor(settings);
        }
        
        /// <summary>
        /// Reset the audio processor (useful when changing microphones)
        /// </summary>
        public void ResetProcessor()
        {
            audioProcessor?.Reset();
        }
        
        /// <summary>
        /// Unity's audio filter callback - processes audio in real-time
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isProcessingEnabled || audioProcessor == null || voiceChatSettings == null)
                return;
                
            // Only process if we have valid audio data
            if (data == null || data.Length == 0)
                return;
            
            // Ensure temp buffer is the right size
            if (tempBuffer == null || tempBuffer.Length != data.Length)
            {
                tempBuffer = new float[data.Length];
            }
            
            // Copy data to temp buffer for processing
            System.Array.Copy(data, tempBuffer, data.Length);
            
            // Process audio based on channel configuration using cached sample rate
            if (channels == 1)
            {
                // Mono audio - process directly
                audioProcessor.ProcessAudio(tempBuffer, cachedSampleRate);
            }
            else if (channels == 2)
            {
                // Stereo audio - process each channel separately
                ProcessStereoAudio(tempBuffer, cachedSampleRate);
            }
            else
            {
                // Multi-channel audio - process as interleaved
                ProcessMultiChannelAudio(tempBuffer, channels, cachedSampleRate);
            }
            
            // Copy processed data back
            System.Array.Copy(tempBuffer, data, data.Length);
        }
        
        private void ProcessStereoAudio(float[] data, int sampleRate)
        {
            // Process left and right channels separately
            for (int i = 0; i < data.Length; i += 2)
            {
                // Create temporary mono samples for processing
                float[] leftSample = { data[i] };
                float[] rightSample = { data[i + 1] };
                
                audioProcessor.ProcessAudio(leftSample, sampleRate);
                audioProcessor.ProcessAudio(rightSample, sampleRate);
                
                data[i] = leftSample[0];     // Left channel
                data[i + 1] = rightSample[0]; // Right channel
            }
        }
        
        private void ProcessMultiChannelAudio(float[] data, int channels, int sampleRate)
        {
            // For multi-channel audio, process each channel separately
            int samplesPerChannel = data.Length / channels;
            
            for (int channel = 0; channel < channels; channel++)
            {
                // Extract samples for this channel
                float[] channelData = new float[samplesPerChannel];
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    channelData[i] = data[i * channels + channel];
                }
                
                // Process the channel
                audioProcessor.ProcessAudio(channelData, sampleRate);
                
                // Put processed samples back
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    data[i * channels + channel] = channelData[i];
                }
            }
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Reinitialize processor if settings change in editor
            if (Application.isPlaying && voiceChatSettings != null)
            {
                audioProcessor = new VoiceChatAudioProcessor(voiceChatSettings);
            }
        }
        #endif
    }
} 