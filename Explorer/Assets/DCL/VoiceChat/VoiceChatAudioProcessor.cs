using DCL.Settings.Settings;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Handles real-time audio processing for voice chat including noise reduction,
    /// noise gate, high-pass filtering, and automatic gain control.
    /// </summary>
    public class VoiceChatAudioProcessor
    {
        private readonly VoiceChatSettingsAsset settings;
        
        // High-pass filter state
        private float highPassPrevInput;
        private float highPassPrevOutput;
        
        // AGC state
        private float currentGain = 1f;
        private float peakLevel;
        
        // Noise gate state
        private float gateSmoothing;
        private float gateHoldTimer;
        private bool gateIsOpen;
        private float lastSpeechTime;
        
        // Noise reduction state
        private readonly float[] noiseProfile;
        private readonly float[] previousSamples;
        private int noiseProfileSamples;
        private const int NOISE_PROFILE_SIZE = 1024;
        private const int NOISE_LEARNING_FRAMES = 30; // Learn noise for first 30 frames
        
        public VoiceChatAudioProcessor(VoiceChatSettingsAsset settings)
        {
            this.settings = settings;
            noiseProfile = new float[NOISE_PROFILE_SIZE];
            previousSamples = new float[NOISE_PROFILE_SIZE];
            Reset();
        }
        
        public void Reset()
        {
            highPassPrevInput = 0f;
            highPassPrevOutput = 0f;
            currentGain = 1f;
            peakLevel = 0f;
            gateSmoothing = 0f;
            noiseProfileSamples = 0;
            gateHoldTimer = 0f;
            gateIsOpen = false;
            lastSpeechTime = 0f;
            
            for (int i = 0; i < noiseProfile.Length; i++)
            {
                noiseProfile[i] = 0f;
                previousSamples[i] = 0f;
            }
        }
        
        /// <summary>
        /// Process audio samples with noise reduction and other effects
        /// </summary>
        public void ProcessAudio(float[] audioData, int sampleRate)
        {
            if (audioData == null || audioData.Length == 0) return;
            
            // Calculate time increment for this audio buffer
            float deltaTime = (float)audioData.Length / sampleRate;
            
            // Learn noise profile from first few frames when no speech is detected
            if (noiseProfileSamples < NOISE_LEARNING_FRAMES && settings.EnableNoiseReduction)
            {
                LearnNoiseProfile(audioData);
            }
            
            for (int i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];
                
                // Apply high-pass filter to remove low-frequency noise
                if (settings.EnableHighPassFilter)
                {
                    sample = ApplyHighPassFilter(sample, sampleRate);
                }
                
                // Apply noise reduction
                if (settings.EnableNoiseReduction && noiseProfileSamples >= NOISE_LEARNING_FRAMES)
                {
                    sample = ApplyNoiseReduction(sample, i);
                }
                
                // Apply noise gate with hold time
                if (settings.EnableNoiseGate)
                {
                    sample = ApplyNoiseGateWithHold(sample, deltaTime);
                }
                
                // Apply automatic gain control
                if (settings.EnableAutoGainControl)
                {
                    sample = ApplyAutoGainControl(sample);
                }
                
                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }
        
        private void LearnNoiseProfile(float[] audioData)
        {
            // Simple noise learning - average the amplitude of quiet samples
            float avgAmplitude = 0f;
            for (int i = 0; i < audioData.Length; i++)
            {
                avgAmplitude += Mathf.Abs(audioData[i]);
            }
            avgAmplitude /= audioData.Length;
            
            // Only learn from quiet samples (likely noise)
            if (avgAmplitude < settings.MicrophoneLoudnessMinimumThreshold * 0.5f)
            {
                int profileIndex = noiseProfileSamples % NOISE_PROFILE_SIZE;
                noiseProfile[profileIndex] = avgAmplitude;
                noiseProfileSamples++;
            }
        }
        
        private float ApplyHighPassFilter(float input, int sampleRate)
        {
            // Simple first-order high-pass filter
            float rc = 1f / (2f * Mathf.PI * settings.HighPassCutoffFreq);
            float dt = 1f / sampleRate;
            float alpha = rc / (rc + dt);
            
            float output = alpha * (highPassPrevOutput + input - highPassPrevInput);
            
            highPassPrevInput = input;
            highPassPrevOutput = output;
            
            return output;
        }
        
        private float ApplyNoiseReduction(float sample, int sampleIndex)
        {
            // Simple spectral subtraction approach
            if (noiseProfileSamples == 0) return sample;
            
            float avgNoise = 0f;
            int profileSamples = Mathf.Min(noiseProfileSamples, NOISE_PROFILE_SIZE);
            
            for (int i = 0; i < profileSamples; i++)
            {
                avgNoise += noiseProfile[i];
            }
            avgNoise /= profileSamples;
            
            float sampleAbs = Mathf.Abs(sample);
            float noiseThreshold = avgNoise * (1f + settings.NoiseReductionStrength);
            
            if (sampleAbs < noiseThreshold)
            {
                // Reduce noise by the specified strength
                float reductionFactor = 1f - settings.NoiseReductionStrength;
                sample *= reductionFactor;
            }
            
            return sample;
        }
        
        private float ApplyNoiseGateWithHold(float sample, float deltaTime)
        {
            float sampleAbs = Mathf.Abs(sample);
            bool speechDetected = sampleAbs > settings.NoiseGateThreshold;
            
            // Update speech detection state
            if (speechDetected)
            {
                lastSpeechTime = 0f; // Reset timer when speech is detected
                gateIsOpen = true;
            }
            else
            {
                lastSpeechTime += deltaTime; // Increment timer when no speech
            }
            
            // Determine if gate should be open based on speech detection and hold time
            bool shouldGateBeOpen = speechDetected || (gateIsOpen && lastSpeechTime < settings.NoiseGateHoldTime);
            
            // Update gate state
            if (shouldGateBeOpen != gateIsOpen)
            {
                gateIsOpen = shouldGateBeOpen;
            }
            
            // Calculate target gate value
            float targetGate = gateIsOpen ? 1f : 0f;
            
            // Apply attack/release timing
            float gateSpeed;
            if (targetGate > gateSmoothing)
            {
                // Opening gate (attack)
                gateSpeed = 1f / (settings.NoiseGateAttackTime * 100f); // Convert to per-sample rate
            }
            else
            {
                // Closing gate (release)
                gateSpeed = 1f / (settings.NoiseGateReleaseTime * 100f); // Convert to per-sample rate
            }
            
            // Smooth the gate transition
            gateSmoothing = Mathf.Lerp(gateSmoothing, targetGate, gateSpeed);
            
            return sample * gateSmoothing;
        }
        
        private float ApplyAutoGainControl(float sample)
        {
            float sampleAbs = Mathf.Abs(sample);
            
            // Update peak level with decay
            float peakDecay = 0.999f;
            peakLevel = Mathf.Max(sampleAbs, peakLevel * peakDecay);
            
            if (peakLevel > 0.001f) // Avoid division by zero
            {
                float targetGain = settings.AGCTargetLevel / peakLevel;
                targetGain = Mathf.Clamp(targetGain, 0.1f, 10f); // Limit gain range
                
                // Smooth gain changes
                float gainSpeed = settings.AGCResponseSpeed * 0.01f;
                currentGain = Mathf.Lerp(currentGain, targetGain, gainSpeed);
            }
            
            return sample * currentGain;
        }
        
        /// <summary>
        /// Get the current noise gate status for UI feedback
        /// </summary>
        public bool IsGateOpen => gateSmoothing > 0.5f;
        
        /// <summary>
        /// Get the current gain level for UI feedback
        /// </summary>
        public float CurrentGain => currentGain;
    }
} 