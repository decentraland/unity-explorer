using DCL.Settings.Settings;
using UnityEngine;
using System;

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
        private float speechLevel; // Track speech-specific levels for AGC
        
        // Noise gate state
        private float gateSmoothing;
        private float gateHoldTimer;
        private bool gateIsOpen;
        private float lastSpeechTime;
        
        // Enhanced noise reduction state
        private readonly float[] noiseProfile;
        private readonly float[] speechProfile;
        private readonly float[] previousSamples;
        private readonly float[] smoothingBuffer;
        private int noiseProfileSamples;
        private int speechProfileSamples;
        private float noiseFloor;
        private float speechFloor;
        private float adaptiveThreshold;
        private bool isLearningNoise = true;
        private float silenceTimer;
        private float speechTimer;
        
        // Frequency analysis for better noise reduction
        private readonly float[] frequencyBins;
        private readonly float[] noiseFrequencyProfile;
        private readonly float[] speechFrequencyProfile;
        private int frequencyAnalysisCounter;
        
        private const int NOISE_PROFILE_SIZE = 1024;
        private const int FREQUENCY_BINS = 32; // Simplified frequency analysis
        private const int NOISE_LEARNING_FRAMES = 60; // Extended learning period
        private const int SPEECH_LEARNING_FRAMES = 30;
        private const float SILENCE_THRESHOLD_TIME = 0.5f; // Time to consider as silence for noise learning
        private const float SPEECH_THRESHOLD_TIME = 0.2f; // Time to consider as speech for speech learning
        
        public VoiceChatAudioProcessor(VoiceChatSettingsAsset settings)
        {
            this.settings = settings;
            noiseProfile = new float[NOISE_PROFILE_SIZE];
            speechProfile = new float[NOISE_PROFILE_SIZE];
            previousSamples = new float[NOISE_PROFILE_SIZE];
            smoothingBuffer = new float[NOISE_PROFILE_SIZE];
            frequencyBins = new float[FREQUENCY_BINS];
            noiseFrequencyProfile = new float[FREQUENCY_BINS];
            speechFrequencyProfile = new float[FREQUENCY_BINS];
            Reset();
        }
        
        public void Reset()
        {
            highPassPrevInput = 0f;
            highPassPrevOutput = 0f;
            currentGain = 1f;
            peakLevel = 0f;
            speechLevel = 0f;
            gateSmoothing = 0f;
            noiseProfileSamples = 0;
            speechProfileSamples = 0;
            noiseFloor = 0f;
            speechFloor = 0f;
            adaptiveThreshold = settings.MicrophoneLoudnessMinimumThreshold;
            isLearningNoise = true;
            silenceTimer = 0f;
            speechTimer = 0f;
            gateHoldTimer = 0f;
            gateIsOpen = false;
            lastSpeechTime = 0f;
            frequencyAnalysisCounter = 0;
            
            for (int i = 0; i < noiseProfile.Length; i++)
            {
                noiseProfile[i] = 0f;
                speechProfile[i] = 0f;
                previousSamples[i] = 0f;
                smoothingBuffer[i] = 0f;
            }
            
            for (int i = 0; i < FREQUENCY_BINS; i++)
            {
                frequencyBins[i] = 0f;
                noiseFrequencyProfile[i] = 0f;
                speechFrequencyProfile[i] = 0f;
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
            
            // Analyze audio characteristics for adaptive processing
            AnalyzeAudioCharacteristics(audioData, deltaTime);
            
            // Learn noise and speech profiles adaptively
            if (settings.EnableNoiseReduction)
            {
                AdaptiveProfileLearning(audioData);
            }
            
            for (int i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];
                
                // Apply high-pass filter to remove low-frequency noise
                if (settings.EnableHighPassFilter)
                {
                    sample = ApplyHighPassFilter(sample, sampleRate);
                }
                
                // Apply noise gate with hold time (early in chain to cut noise before amplification)
                if (settings.EnableNoiseGate)
                {
                    sample = ApplyNoiseGateWithHold(sample, deltaTime);
                }
                
                // Apply automatic gain control with speech-aware processing
                if (settings.EnableAutoGainControl)
                {
                    sample = ApplySpeechAwareAGC(sample);
                }
                
                // Apply enhanced noise reduction AFTER AGC to avoid amplifying artifacts
                if (settings.EnableNoiseReduction && noiseProfileSamples >= NOISE_LEARNING_FRAMES)
                {
                    sample = ApplyEnhancedNoiseReduction(sample, i);
                }
                
                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }
        
        private void AnalyzeAudioCharacteristics(float[] audioData, float deltaTime)
        {
            // Calculate RMS and peak levels
            float rms = 0f;
            float peak = 0f;
            
            for (int i = 0; i < audioData.Length; i++)
            {
                float abs = Mathf.Abs(audioData[i]);
                rms += abs * abs;
                peak = Mathf.Max(peak, abs);
            }
            rms = Mathf.Sqrt(rms / audioData.Length);
            
            // Determine if this is likely speech or noise
            bool likelySpeech = rms > adaptiveThreshold && peak > adaptiveThreshold * 1.5f;
            
            // Update timers
            if (likelySpeech)
            {
                speechTimer += deltaTime;
                silenceTimer = 0f;
            }
            else
            {
                silenceTimer += deltaTime;
                speechTimer = 0f;
            }
            
            // Update adaptive threshold based on recent activity
            if (silenceTimer > SILENCE_THRESHOLD_TIME)
            {
                noiseFloor = Mathf.Lerp(noiseFloor, rms, 0.1f);
                adaptiveThreshold = Mathf.Max(noiseFloor * 2f, settings.MicrophoneLoudnessMinimumThreshold);
            }
            else if (speechTimer > SPEECH_THRESHOLD_TIME)
            {
                speechFloor = Mathf.Lerp(speechFloor, rms, 0.05f);
            }
        }
        
        private void AdaptiveProfileLearning(float[] audioData)
        {
            // Calculate current audio characteristics
            float avgAmplitude = 0f;
            for (int i = 0; i < audioData.Length; i++)
            {
                avgAmplitude += Mathf.Abs(audioData[i]);
            }
            avgAmplitude /= audioData.Length;
            
            // Learn noise profile during silence
            if (silenceTimer > SILENCE_THRESHOLD_TIME && noiseProfileSamples < NOISE_LEARNING_FRAMES * 2)
            {
                int profileIndex = noiseProfileSamples % NOISE_PROFILE_SIZE;
                noiseProfile[profileIndex] = avgAmplitude;
                noiseProfileSamples++;
                
                // Update frequency-based noise profile
                UpdateFrequencyProfile(audioData, noiseFrequencyProfile, 0.1f);
            }
            
            // Learn speech profile during speech
            if (speechTimer > SPEECH_THRESHOLD_TIME && speechProfileSamples < SPEECH_LEARNING_FRAMES && avgAmplitude > adaptiveThreshold)
            {
                int profileIndex = speechProfileSamples % NOISE_PROFILE_SIZE;
                speechProfile[profileIndex] = avgAmplitude;
                speechProfileSamples++;
                
                // Update frequency-based speech profile
                UpdateFrequencyProfile(audioData, speechFrequencyProfile, 0.05f);
            }
        }
        
        private void UpdateFrequencyProfile(float[] audioData, float[] profile, float learningRate)
        {
            // Simple frequency analysis using overlapping windows
            int windowSize = audioData.Length / FREQUENCY_BINS;
            
            for (int bin = 0; bin < FREQUENCY_BINS; bin++)
            {
                float binEnergy = 0f;
                int startIdx = bin * windowSize;
                int endIdx = Mathf.Min(startIdx + windowSize, audioData.Length);
                
                for (int i = startIdx; i < endIdx; i++)
                {
                    binEnergy += audioData[i] * audioData[i];
                }
                
                binEnergy = Mathf.Sqrt(binEnergy / (endIdx - startIdx));
                profile[bin] = Mathf.Lerp(profile[bin], binEnergy, learningRate);
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
        
        private float ApplyEnhancedNoiseReduction(float sample, int sampleIndex)
        {
            if (noiseProfileSamples == 0) return sample;
            
            // Calculate noise floor from profile
            float avgNoise = 0f;
            int profileSamples = Mathf.Min(noiseProfileSamples, NOISE_PROFILE_SIZE);
            
            for (int i = 0; i < profileSamples; i++)
            {
                avgNoise += noiseProfile[i];
            }
            avgNoise /= profileSamples;
            
            // Calculate speech floor if available
            float avgSpeech = avgNoise * 3f; // More conservative default fallback
            if (speechProfileSamples > 0)
            {
                avgSpeech = 0f;
                int speechSamples = Mathf.Min(speechProfileSamples, NOISE_PROFILE_SIZE);
                for (int i = 0; i < speechSamples; i++)
                {
                    avgSpeech += speechProfile[i];
                }
                avgSpeech /= speechSamples;
            }
            
            float sampleAbs = Mathf.Abs(sample);
            float originalSample = sample;
            
            // Gentler, artifact-resistant noise reduction
            
            // Stage 1: Conservative noise floor reduction (much gentler)
            float noiseThreshold = avgNoise * (1f + settings.NoiseReductionStrength * 0.2f); // Reduced multiplier
            if (sampleAbs < noiseThreshold)
            {
                // Much gentler reduction to avoid artifacts
                float reductionFactor = 1f - settings.NoiseReductionStrength * 0.3f; // Reduced from 0.8f
                sample *= reductionFactor;
                sampleAbs *= reductionFactor;
            }
            
            // Stage 2: Speech preservation check
            if (avgSpeech > avgNoise * 1.5f) // We have learned speech patterns
            {
                float speechRatio = sampleAbs / avgSpeech;
                
                // If this looks like speech, reduce noise reduction strength
                if (speechRatio > 0.3f) // This might be speech
                {
                    float speechProtection = Mathf.Clamp01(speechRatio);
                    sample = Mathf.Lerp(sample, originalSample, speechProtection * 0.5f);
                }
            }
            
            // Stage 3: Frequency-aware reduction (simplified)
            if (frequencyAnalysisCounter % 16 == 0) // Update less frequently for stability
            {
                UpdateFrequencyBins(sample, sampleIndex);
            }
            
            // Stage 4: Gentle smoothing only for very quiet samples
            int bufferIndex = sampleIndex % NOISE_PROFILE_SIZE;
            smoothingBuffer[bufferIndex] = sample;
            
            if (sampleIndex > 4 && sampleAbs < avgNoise * 1.5f) // Only smooth very quiet samples
            {
                int prevIndex1 = (sampleIndex - 1) % NOISE_PROFILE_SIZE;
                int prevIndex2 = (sampleIndex - 2) % NOISE_PROFILE_SIZE;
                
                // Very gentle 3-point smoothing
                float smoothed = (sample * 0.6f + smoothingBuffer[prevIndex1] * 0.3f + smoothingBuffer[prevIndex2] * 0.1f);
                sample = smoothed;
            }
            
            // Stage 5: Artifact prevention - limit how much we can change the signal
            float maxChange = originalSample * 0.5f; // Don't change signal by more than 50%
            float change = sample - originalSample;
            if (Mathf.Abs(change) > maxChange)
            {
                sample = originalSample + Mathf.Sign(change) * maxChange;
            }
            
            frequencyAnalysisCounter++;
            return sample;
        }
        
        private void UpdateFrequencyBins(float sample, int sampleIndex)
        {
            // Simple frequency bin update for real-time processing
            int binIndex = (sampleIndex / 8) % FREQUENCY_BINS;
            frequencyBins[binIndex] = Mathf.Lerp(frequencyBins[binIndex], Mathf.Abs(sample), 0.1f);
        }
        
        private float ApplyNoiseGateWithHold(float sample, float deltaTime)
        {
            float sampleAbs = Mathf.Abs(sample);
            
            // Use the lower of the two thresholds to be more permissive for speech
            float effectiveThreshold = Mathf.Min(settings.NoiseGateThreshold, adaptiveThreshold * 0.3f);
            // But ensure we don't go below the configured threshold
            effectiveThreshold = Mathf.Max(effectiveThreshold, settings.NoiseGateThreshold * 0.5f);
            
            bool speechDetected = sampleAbs > effectiveThreshold;
            
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
        
        private float ApplySpeechAwareAGC(float sample)
        {
            float sampleAbs = Mathf.Abs(sample);
            
            // Update peak level with decay
            float peakDecay = 0.999f;
            peakLevel = Mathf.Max(sampleAbs, peakLevel * peakDecay);
            
            // Track speech-specific levels separately to avoid amplifying noise
            if (speechTimer > SPEECH_THRESHOLD_TIME || sampleAbs > adaptiveThreshold)
            {
                speechLevel = Mathf.Max(sampleAbs, speechLevel * 0.995f);
            }
            else
            {
                speechLevel *= 0.998f; // Faster decay when not speaking
            }
            
            // Use speech level for AGC calculation when available, otherwise fall back to peak level
            float referenceLevel = speechLevel > 0.001f ? speechLevel : peakLevel;
            
            if (referenceLevel > 0.001f) // Avoid division by zero
            {
                float targetGain = settings.AGCTargetLevel / referenceLevel;
                
                // More conservative gain limiting to prevent noise amplification
                float maxGain = speechLevel > 0.001f ? 5f : 2f; // Lower max gain when no clear speech
                targetGain = Mathf.Clamp(targetGain, 0.1f, maxGain);
                
                // Slower gain adjustment to prevent pumping artifacts
                float gainSpeed = settings.AGCResponseSpeed * 0.005f; // Reduced from 0.01f
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
        
        /// <summary>
        /// Get the current noise floor level for debugging
        /// </summary>
        public float NoiseFloor => noiseFloor;
        
        /// <summary>
        /// Get the current speech floor level for debugging
        /// </summary>
        public float SpeechFloor => speechFloor;
        
        /// <summary>
        /// Get whether the system is currently learning noise patterns
        /// </summary>
        public bool IsLearningNoise => isLearningNoise && noiseProfileSamples < NOISE_LEARNING_FRAMES;
        
        /// <summary>
        /// Get the current adaptive threshold for debugging
        /// </summary>
        public float AdaptiveThreshold => adaptiveThreshold;
        
        /// <summary>
        /// Get the current gate smoothing value for debugging
        /// </summary>
        public float GateSmoothing => gateSmoothing;
    }
} 