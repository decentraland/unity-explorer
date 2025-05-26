using DCL.Settings.Settings;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Handles real-time audio processing for voice chat including noise reduction,
    ///     noise gate, high-pass filtering, and automatic gain control.
    /// </summary>
    public class VoiceChatAudioProcessor
    {
        private readonly VoiceChatSettingsAsset settings;

        private float highPassPrevInput;
        private float highPassPrevOutput;

        // AGC state
        private float peakLevel;

        // Noise gate state
        private bool gateIsOpen;
        private float lastSpeechTime;

        // Simplified audio analysis

        // Native arrays for Burst compilation (allocated once, reused)
        private NativeArray<float> nativeAudioBuffer;
        private NativeArray<float> nativeSharedState;
        private NativeArray<float> nativeAnalysisResults;
        private JobHandle lastJobHandle;
        private bool useJobSystem = true; // Can be toggled for debugging

        /// <summary>
        ///     Get the current noise gate status for UI feedback
        /// </summary>
        public bool IsGateOpen => GateSmoothing > 0.5f;

        /// <summary>
        ///     Get the current gain level for UI feedback
        /// </summary>
        public float CurrentGain { get; private set; } = 1f;

        /// <summary>
        ///     Get the current adaptive threshold for debugging
        /// </summary>
        public float AdaptiveThreshold { get; private set; }

        /// <summary>
        ///     Get the current gate smoothing value for debugging
        /// </summary>
        public float GateSmoothing { get; private set; }

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
            CurrentGain = 1f;
            peakLevel = 0f;
            GateSmoothing = 0f;
            AdaptiveThreshold = settings.MicrophoneLoudnessMinimumThreshold;
            gateIsOpen = false;
            lastSpeechTime = 0f;

            // Reset native arrays
            if (nativeSharedState.IsCreated)
            {
                nativeSharedState[0] = CurrentGain;
                nativeSharedState[1] = peakLevel;
                nativeSharedState[2] = GateSmoothing;
            }
        }

        /// <summary>
        ///     Enable or disable the Burst-compiled job system for audio processing
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
        ///     Process audio samples with noise reduction and other effects
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
            if (useJobSystem && CanUseBurstPath(audioData.Length)) { ProcessAudioBurst(audioData, sampleRate); }
            else
            {
                // Fallback to original implementation for compatibility
                ProcessAudioFallback(audioData, sampleRate);
            }
        }

        private bool CanUseBurstPath(int audioLength)
        {
            // Check if Burst is actually available and enabled
            if (!BurstCompiler.IsEnabled) return false;

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
        ///     Optimized audio processing using Burst-compiled jobs with block processing for stateful filters
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
            nativeSharedState[0] = CurrentGain;
            nativeSharedState[1] = peakLevel;
            nativeSharedState[2] = GateSmoothing;
            nativeSharedState[3] = highPassPrevInput;
            nativeSharedState[4] = highPassPrevOutput;
            nativeSharedState[5] = lastSpeechTime;
            nativeSharedState[6] = gateIsOpen ? 1f : 0f;

            // Schedule audio analysis job
            var analysisJob = new AudioAnalysisJob
            {
                audioData = nativeAudioBuffer.GetSubArray(0, audioLength),
                analysisResults = nativeAnalysisResults,
            };

            JobHandle analysisHandle = analysisJob.Schedule();

            // Use block processing for stateful operations (high-pass filter)
            // Process in blocks to maintain filter state continuity
            const int blockSize = 256; // Good balance between state continuity and parallelization
            JobHandle lastHandle = analysisHandle;

            for (var blockStart = 0; blockStart < audioLength; blockStart += blockSize)
            {
                int currentBlockSize = math.min(blockSize, audioLength - blockStart);

                var blockProcessingJob = new AudioBlockProcessingJob
                {
                    audioData = nativeAudioBuffer.GetSubArray(blockStart, currentBlockSize),
                    enableHighPassFilter = settings.EnableHighPassFilter,
                    enableNoiseGate = settings.EnableNoiseGate,
                    enableAutoGainControl = settings.EnableAutoGainControl,
                    highPassCutoffFreq = settings.HighPassCutoffFreq,
                    noiseGateThreshold = settings.NoiseGateThreshold,
                    noiseGateHoldTime = settings.NoiseGateHoldTime,
                    noiseGateAttackTime = settings.NoiseGateAttackTime,
                    noiseGateReleaseTime = settings.NoiseGateReleaseTime,
                    agcTargetLevel = settings.AGCTargetLevel,
                    agcResponseSpeed = settings.AGCResponseSpeed,
                    sampleRate = sampleRate,
                    sharedState = nativeSharedState,
                    adaptiveThreshold = AdaptiveThreshold,
                };

                lastHandle = blockProcessingJob.Schedule(lastHandle);
            }

            // Set the processing handle as the last job handle
            lastJobHandle = lastHandle;

            // Complete jobs and copy results back
            lastJobHandle.Complete();
            lastJobHandle = default(JobHandle);

            // Copy processed audio back to managed array
            NativeArray<float>.Copy(nativeAudioBuffer, 0, audioData, 0, audioLength);

            // Update managed state from native arrays
            CurrentGain = nativeSharedState[0];
            peakLevel = nativeSharedState[1];
            GateSmoothing = nativeSharedState[2];
            highPassPrevInput = nativeSharedState[3];
            highPassPrevOutput = nativeSharedState[4];
            lastSpeechTime = nativeSharedState[5];
            gateIsOpen = nativeSharedState[6] > 0.5f;
        }

        /// <summary>
        ///     Fallback audio processing using original implementation
        /// </summary>
        private void ProcessAudioFallback(float[] audioData, int sampleRate)
        {
            // Calculate time increment for this audio buffer
            float deltaTime = (float)audioData.Length / sampleRate;

            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                // Apply high-pass filter to remove low-frequency noise
                if (settings.EnableHighPassFilter) { sample = ApplyHighPassFilter(sample, sampleRate); }

                // Apply noise gate with hold time
                if (settings.EnableNoiseGate) { sample = ApplyNoiseGateWithHold(sample, deltaTime); }

                // Apply automatic gain control
                if (settings.EnableAutoGainControl) { sample = ApplyAGC(sample); }

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
            float effectiveThreshold = Mathf.Min(settings.NoiseGateThreshold, AdaptiveThreshold * 0.3f);

            // But ensure we don't go below the configured threshold
            effectiveThreshold = Mathf.Max(effectiveThreshold, settings.NoiseGateThreshold * 0.5f);

            bool speechDetected = sampleAbs > effectiveThreshold;

            if (speechDetected)
            {
                lastSpeechTime = 0f;
                gateIsOpen = true;
            }
            else
                lastSpeechTime += deltaTime;

            bool shouldGateBeOpen = speechDetected || (gateIsOpen && lastSpeechTime < settings.NoiseGateHoldTime);

            gateIsOpen = shouldGateBeOpen;

            // Calculate target gate value
            float targetGate = gateIsOpen ? 1f : 0f;

            // Apply attack/release timing
            float gateSpeed;

            if (targetGate > GateSmoothing)
            {
                // Opening gate (attack)
                gateSpeed = 1f / (settings.NoiseGateAttackTime * 100f); // Convert to per-sample rate
            }
            else
            {
                // Closing gate (release)
                gateSpeed = 1f / (settings.NoiseGateReleaseTime * 100f); // Convert to per-sample rate
            }

            GateSmoothing = Mathf.Lerp(GateSmoothing, targetGate, gateSpeed);

            return sample * GateSmoothing;
        }

        private float ApplyAGC(float sample)
        {
            float sampleAbs = Mathf.Abs(sample);

            // Update peak level with decay
            var peakDecay = 0.999f;
            peakLevel = Mathf.Max(sampleAbs, peakLevel * peakDecay);

            if (peakLevel > 0.001f) // Avoid division by zero
            {
                float targetGain = settings.AGCTargetLevel / peakLevel;
                targetGain = Mathf.Clamp(targetGain, 0.1f, 5f);

                // Smooth gain adjustment to prevent pumping artifacts
                float gainSpeed = settings.AGCResponseSpeed * 0.005f;
                CurrentGain = Mathf.Lerp(CurrentGain, targetGain, gainSpeed);
            }

            return sample * CurrentGain;
        }
    }

    /// <summary>
    ///     Burst-compiled job for block-based audio processing with proper state management
    /// </summary>
    [BurstCompile]
    public struct AudioBlockProcessingJob : IJob
    {
        // Input/Output
        public NativeArray<float> audioData;

        // Settings (read-only)
        [ReadOnly] public bool enableHighPassFilter;
        [ReadOnly] public bool enableNoiseGate;
        [ReadOnly] public bool enableAutoGainControl;
        [ReadOnly] public float highPassCutoffFreq;
        [ReadOnly] public float noiseGateThreshold;
        [ReadOnly] public float noiseGateHoldTime;
        [ReadOnly] public float noiseGateAttackTime;
        [ReadOnly] public float noiseGateReleaseTime;
        [ReadOnly] public float agcTargetLevel;
        [ReadOnly] public float agcResponseSpeed;
        [ReadOnly] public int sampleRate;

        // Shared state - [0]=currentGain, [1]=peakLevel, [2]=gateSmoothing, [3]=highPassPrevInput, [4]=highPassPrevOutput, [5]=lastSpeechTime, [6]=gateIsOpen
        public NativeArray<float> sharedState;

        [ReadOnly] public float adaptiveThreshold;

        public void Execute()
        {
            // Load filter state
            float currentGain = sharedState[0];
            float peakLevel = sharedState[1];
            float gateSmoothing = sharedState[2];
            float highPassPrevInput = sharedState[3];
            float highPassPrevOutput = sharedState[4];
            float lastSpeechTime = sharedState[5];
            bool gateIsOpen = sharedState[6] > 0.5f;

            // Calculate delta time for this block
            float deltaTime = (float)audioData.Length / sampleRate;

            // Process each sample sequentially to maintain filter state
            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                if (enableHighPassFilter) { sample = ApplyHighPassFilterBurst(sample, ref highPassPrevInput, ref highPassPrevOutput); }

                if (enableNoiseGate) { sample = ApplyNoiseGateBurst(sample, ref gateSmoothing, ref lastSpeechTime, ref gateIsOpen, deltaTime); }

                if (enableAutoGainControl) { sample = ApplyAGCBurst(sample, ref currentGain, ref peakLevel); }

                audioData[i] = math.clamp(sample, -1f, 1f);
            }

            // Save filter state back
            sharedState[0] = currentGain;
            sharedState[1] = peakLevel;
            sharedState[2] = gateSmoothing;
            sharedState[3] = highPassPrevInput;
            sharedState[4] = highPassPrevOutput;
            sharedState[5] = lastSpeechTime;
            sharedState[6] = gateIsOpen ? 1f : 0f;
        }

        private float ApplyHighPassFilterBurst(float input, ref float prevInput, ref float prevOutput)
        {
            // Proper high-pass filter with state management
            float rc = 1f / (2f * math.PI * highPassCutoffFreq);
            float dt = 1f / sampleRate;
            float alpha = rc / (rc + dt);

            float output = alpha * (prevOutput + input - prevInput);

            prevInput = input;
            prevOutput = output;

            return output;
        }

        private float ApplyNoiseGateBurst(float sample, ref float gateSmoothing, ref float lastSpeechTime, ref bool gateIsOpen, float deltaTime)
        {
            float sampleAbs = math.abs(sample);
            
            // Use the same adaptive threshold logic as fallback
            float effectiveThreshold = math.max(noiseGateThreshold * 0.5f,
                                              math.min(noiseGateThreshold, adaptiveThreshold * 0.3f));

            bool speechDetected = sampleAbs > effectiveThreshold;

            // Update speech detection state with proper timing
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
            bool shouldGateBeOpen = speechDetected || (gateIsOpen && lastSpeechTime < noiseGateHoldTime);

            // Update gate state
            gateIsOpen = shouldGateBeOpen;

            // Calculate target gate value
            float targetGate = gateIsOpen ? 1f : 0f;

            // Apply proper attack/release timing from settings
            float gateSpeed;
            if (targetGate > gateSmoothing)
            {
                // Opening gate (attack) - convert from settings to per-sample rate
                gateSpeed = 1f / (noiseGateAttackTime * 100f);
            }
            else
            {
                // Closing gate (release) - convert from settings to per-sample rate  
                gateSpeed = 1f / (noiseGateReleaseTime * 100f);
            }

            gateSmoothing = math.lerp(gateSmoothing, targetGate, gateSpeed);

            return sample * gateSmoothing;
        }

        private float ApplyAGCBurst(float sample, ref float currentGain, ref float peakLevel)
        {
            float sampleAbs = math.abs(sample);

            // Update peak level with decay
            var peakDecay = 0.999f;
            peakLevel = math.max(sampleAbs, peakLevel * peakDecay);

            if (peakLevel > 0.001f)
            {
                float targetGain = agcTargetLevel / peakLevel;
                targetGain = math.clamp(targetGain, 0.1f, 5f);

                float gainSpeed = agcResponseSpeed * 0.005f;
                currentGain = math.lerp(currentGain, targetGain, gainSpeed);
            }

            return sample * currentGain;
        }
    }

    /// <summary>
    ///     Burst-compiled job for vectorized audio analysis
    /// </summary>
    [BurstCompile]
    public struct AudioAnalysisJob : IJob
    {
        [ReadOnly] public NativeArray<float> audioData;
        public NativeArray<float> analysisResults; // [0]=rms, [1]=peak, [2]=avgAmplitude

        public void Execute()
        {
            var rms = 0f;
            var peak = 0f;
            var avgAmplitude = 0f;
            int length = audioData.Length;

            // Vectorized processing with loop unrolling
            for (var i = 0; i < length; i += 4)
            {
                int remaining = math.min(4, length - i);

                for (var j = 0; j < remaining; j++)
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
