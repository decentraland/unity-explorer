using DCL.Settings.Settings;
using UnityEngine;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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

        // Simplified audio analysis
        private float adaptiveThreshold;

        // Native arrays for Burst compilation (allocated once, reused)
        private NativeArray<float> nativeAudioBuffer;
        private NativeArray<float> nativeSharedState;
        private NativeArray<float> nativeAnalysisResults;
        private JobHandle lastJobHandle;
        private bool useJobSystem = true; // Can be toggled for debugging

        // LiveKit constraints
        private const int DEFAULT_NUM_CHANNELS = 2;
        private const int DEFAULT_SAMPLE_RATE = 48000;

        public VoiceChatAudioProcessor(VoiceChatSettingsAsset settings)
        {
            this.settings = settings;

            // Initialize native arrays for Burst compilation
            InitializeNativeArrays();
            Reset();
        }

        private void InitializeNativeArrays()
        {
            // Initialize with reasonable default sizes - will be resized as needed
            nativeAudioBuffer = new NativeArray<float>(4096, Allocator.Persistent);
            nativeSharedState = new NativeArray<float>(8, Allocator.Persistent); // [0]=currentGain, [1]=peakLevel, [2]=gateSmoothing, etc.
            nativeAnalysisResults = new NativeArray<float>(8, Allocator.Persistent); // [0]=rms, [1]=peak, [2]=avgAmplitude, etc.
        }

                        public void Dispose()
        {
            // Complete any pending jobs before disposing
            if (!lastJobHandle.Equals(default(JobHandle)))
            {
                lastJobHandle.Complete();
                lastJobHandle = default(JobHandle);
            }
                
            // Dispose native arrays
            if (nativeAudioBuffer.IsCreated) nativeAudioBuffer.Dispose();
            if (nativeSharedState.IsCreated) nativeSharedState.Dispose();
            if (nativeAnalysisResults.IsCreated) nativeAnalysisResults.Dispose();
        }

                        public void Reset()
        {
            // Complete any pending jobs before resetting
            if (!lastJobHandle.Equals(default(JobHandle)))
            {
                lastJobHandle.Complete();
                lastJobHandle = default(JobHandle);
            }
                
            highPassPrevInput = 0f;
            highPassPrevOutput = 0f;
            currentGain = 1f;
            peakLevel = 0f;
            gateSmoothing = 0f;
            adaptiveThreshold = settings.MicrophoneLoudnessMinimumThreshold;
            gateHoldTimer = 0f;
            gateIsOpen = false;
            lastSpeechTime = 0f;

            // Reset native arrays
            if (nativeSharedState.IsCreated)
            {
                nativeSharedState[0] = currentGain;
                nativeSharedState[1] = peakLevel;
                nativeSharedState[2] = gateSmoothing;
            }
        }

        /// <summary>
        /// Enable or disable the Burst-compiled job system for audio processing
        /// </summary>
        public void SetUseJobSystem(bool enabled)
        {
            // Complete any pending jobs before changing mode
            if (!lastJobHandle.Equals(default(JobHandle)))
            {
                lastJobHandle.Complete();
                lastJobHandle = default(JobHandle);
            }
                
            useJobSystem = enabled;
        }

        /// <summary>
        /// Process audio samples with noise reduction and other effects
        /// </summary>
        public void ProcessAudio(float[] audioData, int sampleRate)
        {
            if (audioData == null || audioData.Length == 0) return;

            // Complete any previous job before starting new processing
            if (!lastJobHandle.Equals(default(JobHandle)))
            {
                lastJobHandle.Complete();
                lastJobHandle = default(JobHandle);
            }

            // Use optimized Burst-compiled path when possible
            if (useJobSystem && CanUseBurstPath(audioData.Length))
            {
                ProcessAudioBurst(audioData, sampleRate);
            }
            else
            {
                // Fallback to original implementation for compatibility
                ProcessAudioFallback(audioData, sampleRate);
            }
        }

        private bool CanUseBurstPath(int audioLength)
        {
            // Check if Burst is actually available and enabled
            if (!Unity.Burst.BurstCompiler.IsEnabled) return false;

            // macOS-specific compatibility checks
            #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // On macOS, be more conservative with Burst usage due to Core Audio sensitivity
            // Disable Burst for very small buffers to avoid Core Audio timing issues
            if (audioLength < 256) return false; // Increased from 128 for macOS
            if (audioLength > 4096) return false; // Reduced from 8192 for macOS stability
            
            // Additional check for Apple Silicon compatibility
            if (SystemInfo.processorType.Contains("Apple")) 
            {
                // Even more conservative on Apple Silicon due to potential Rosetta issues
                if (audioLength < 512) return false;
            }
            #else
            // Size efficiency thresholds - Burst overhead isn't worth it for tiny buffers
            if (audioLength < 128) return false; // Increased minimum for better efficiency
            if (audioLength > 8192) return false; // Reduced maximum for memory constraints
            #endif

            return true;
        }

        /// <summary>
        /// Optimized audio processing using Burst-compiled jobs
        /// </summary>
        private void ProcessAudioBurst(float[] audioData, int sampleRate)
        {
            int audioLength = audioData.Length;

            // Resize native buffer if needed
            if (nativeAudioBuffer.Length < audioLength)
            {
                nativeAudioBuffer.Dispose();
                nativeAudioBuffer = new NativeArray<float>(audioLength, Allocator.Persistent);
            }

            // Copy audio data to native array
            NativeArray<float>.Copy(audioData, nativeAudioBuffer, audioLength);

            // Update shared state
            nativeSharedState[0] = currentGain;
            nativeSharedState[1] = peakLevel;
            nativeSharedState[2] = gateSmoothing;

            // Schedule audio analysis job
            var analysisJob = new AudioAnalysisJob
            {
                audioData = nativeAudioBuffer.GetSubArray(0, audioLength),
                analysisResults = nativeAnalysisResults
            };
            JobHandle analysisHandle = analysisJob.Schedule();

            // Schedule main audio processing job
            var processingJob = new AudioProcessingJob
            {
                audioData = nativeAudioBuffer.GetSubArray(0, audioLength),
                enableHighPassFilter = settings.EnableHighPassFilter,
                enableNoiseGate = settings.EnableNoiseGate,
                enableAutoGainControl = settings.EnableAutoGainControl,
                highPassCutoffFreq = settings.HighPassCutoffFreq,
                noiseGateThreshold = settings.NoiseGateThreshold,
                agcTargetLevel = settings.AGCTargetLevel,
                agcResponseSpeed = settings.AGCResponseSpeed,
                sampleRate = sampleRate,
                sharedState = nativeSharedState,
                adaptiveThreshold = adaptiveThreshold
            };

                        // Use parallel processing for larger buffers
            int batchSize = math.max(32, audioLength / 8);
            JobHandle processingHandle = processingJob.Schedule(audioLength, batchSize, analysisHandle);

            // Set the processing handle as the last job handle
            lastJobHandle = processingHandle;
            
            // Complete jobs and copy results back
            lastJobHandle.Complete();
            lastJobHandle = default(JobHandle);

            // Copy processed audio back to managed array
            NativeArray<float>.Copy(nativeAudioBuffer, 0, audioData, 0, audioLength);

            // Update managed state from native arrays
            currentGain = nativeSharedState[0];
            peakLevel = nativeSharedState[1];
            gateSmoothing = nativeSharedState[2];

        }

        /// <summary>
        /// Fallback audio processing using original implementation
        /// </summary>
        private void ProcessAudioFallback(float[] audioData, int sampleRate)
        {
            // Calculate time increment for this audio buffer
            float deltaTime = (float)audioData.Length / sampleRate;

            for (int i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                // Apply high-pass filter to remove low-frequency noise
                if (settings.EnableHighPassFilter)
                {
                    sample = ApplyHighPassFilter(sample, sampleRate);
                }

                // Apply noise gate with hold time
                if (settings.EnableNoiseGate)
                {
                    sample = ApplyNoiseGateWithHold(sample, deltaTime);
                }

                // Apply automatic gain control
                if (settings.EnableAutoGainControl)
                {
                    sample = ApplyAGC(sample);
                }

                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }



        private float ApplyHighPassFilter(float input, int sampleRate)
        {
            // Simple first-order high-pass filter optimized for 48kHz
            float rc = 1f / (2f * Mathf.PI * settings.HighPassCutoffFreq);
            float dt = 1f / sampleRate; // Will typically be 1/48000
            float alpha = rc / (rc + dt);

            float output = alpha * (highPassPrevOutput + input - highPassPrevInput);

            highPassPrevInput = input;
            highPassPrevOutput = output;

            return output;
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

        private float ApplyAGC(float sample)
        {
            float sampleAbs = Mathf.Abs(sample);

            // Update peak level with decay
            float peakDecay = 0.999f;
            peakLevel = Mathf.Max(sampleAbs, peakLevel * peakDecay);

            if (peakLevel > 0.001f) // Avoid division by zero
            {
                float targetGain = settings.AGCTargetLevel / peakLevel;
                targetGain = Mathf.Clamp(targetGain, 0.1f, 5f);

                // Smooth gain adjustment to prevent pumping artifacts
                float gainSpeed = settings.AGCResponseSpeed * 0.005f;
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
        /// Get the current adaptive threshold for debugging
        /// </summary>
        public float AdaptiveThreshold => adaptiveThreshold;

        /// <summary>
        /// Get the current gate smoothing value for debugging
        /// </summary>
        public float GateSmoothing => gateSmoothing;
    }

    /// <summary>
    /// Burst-compiled job for high-performance audio processing
    /// </summary>
    [BurstCompile]
    public struct AudioProcessingJob : IJobParallelFor
    {
        // Input/Output
        public NativeArray<float> audioData;

        // Settings (read-only)
        [ReadOnly] public bool enableHighPassFilter;
        [ReadOnly] public bool enableNoiseGate;
        [ReadOnly] public bool enableAutoGainControl;
        [ReadOnly] public float highPassCutoffFreq;
        [ReadOnly] public float noiseGateThreshold;
        [ReadOnly] public float agcTargetLevel;
        [ReadOnly] public float agcResponseSpeed;
        [ReadOnly] public int sampleRate;

        // Shared state (atomic operations where needed)
        public NativeArray<float> sharedState; // [0]=currentGain, [1]=peakLevel, [2]=gateSmoothing

        [ReadOnly] public float adaptiveThreshold;

        public void Execute(int index)
        {
            float sample = audioData[index];

            // High-pass filter (vectorizable)
            if (enableHighPassFilter)
            {
                sample = ApplyHighPassFilterBurst(sample);
            }

            // Noise gate (optimized)
            if (enableNoiseGate)
            {
                sample = ApplyNoiseGateBurst(sample);
            }

            // AGC (optimized)
            if (enableAutoGainControl)
            {
                sample = ApplyAGCBurst(sample);
            }

            audioData[index] = math.clamp(sample, -1f, 1f);
        }

        private float ApplyHighPassFilterBurst(float input)
        {
            // Simplified high-pass filter for burst compilation, optimized for 48kHz
            float rc = 1f / (2f * math.PI * highPassCutoffFreq);
            float dt = 1f / sampleRate; // Typically 1/48000
            float alpha = rc / (rc + dt);

            // Note: This is a simplified version - full state would need to be managed differently
            return input * alpha;
        }



        private float ApplyNoiseGateBurst(float sample)
        {
            float sampleAbs = math.abs(sample);
            float effectiveThreshold = math.max(noiseGateThreshold * 0.5f,
                                              math.min(noiseGateThreshold, adaptiveThreshold * 0.3f));

            bool speechDetected = sampleAbs > effectiveThreshold;
            float gateValue = speechDetected ? 1f : 0f;

            // Simplified gating for burst compilation
            return sample * gateValue;
        }

        private float ApplyAGCBurst(float sample)
        {
            float sampleAbs = math.abs(sample);
            float currentGain = sharedState[0];

            if (sampleAbs > 0.001f)
            {
                float targetGain = agcTargetLevel / sampleAbs;
                targetGain = math.clamp(targetGain, 0.1f, 5f);

                float gainSpeed = agcResponseSpeed * 0.005f;
                currentGain = math.lerp(currentGain, targetGain, gainSpeed);
                sharedState[0] = currentGain;
            }

            return sample * currentGain;
        }
    }

    /// <summary>
    /// Burst-compiled job for vectorized audio analysis
    /// </summary>
    [BurstCompile]
    public struct AudioAnalysisJob : IJob
    {
        [ReadOnly] public NativeArray<float> audioData;
        public NativeArray<float> analysisResults; // [0]=rms, [1]=peak, [2]=avgAmplitude

        public void Execute()
        {
            float rms = 0f;
            float peak = 0f;
            float avgAmplitude = 0f;
            int length = audioData.Length;

            // Vectorized processing with loop unrolling
            for (int i = 0; i < length; i += 4)
            {
                int remaining = math.min(4, length - i);

                for (int j = 0; j < remaining; j++)
                {
                    float sample = audioData[i + j];
                    float abs = math.abs(sample);

                    rms += sample * sample;
                    peak = math.max(peak, abs);
                    avgAmplitude += abs;
                }
            }

            rms = math.sqrt(rms / length);
            avgAmplitude /= length;

            analysisResults[0] = rms;
            analysisResults[1] = peak;
            analysisResults[2] = avgAmplitude;
        }
    }


}
