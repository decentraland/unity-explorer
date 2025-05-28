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
        private readonly VoiceChatConfiguration configuration;

        // Band-pass filter state (2nd order Butterworth filters)
        private readonly float[] highPassPrevInputs = new float[2];
        private readonly float[] highPassPrevOutputs = new float[2];
        private readonly float[] lowPassPrevInputs = new float[2];
        private readonly float[] lowPassPrevOutputs = new float[2];

        // DC blocking filter state (simple high-pass at ~20Hz)
        private float dcBlockPrevInput;
        private float dcBlockPrevOutput;

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

        /// <summary>
        ///     Get debug information about the current audio processing state
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Gate: {(IsGateOpen ? "OPEN" : "CLOSED")} ({GateSmoothing:F3}), " +
                   $"Gain: {CurrentGain:F2}x, " +
                   $"Peak: {peakLevel:F3}, " +
                   $"Threshold: {AdaptiveThreshold:F3}, " +
                   $"Burst: {(useJobSystem ? "ON" : "OFF")}";
        }

        /// <summary>
        ///     Toggle between Burst and fallback processing for debugging
        /// </summary>
        public void SetBurstProcessing(bool enabled)
        {
            useJobSystem = enabled;
        }

        public VoiceChatAudioProcessor(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;

            // Initialize native arrays for Burst compilation
            InitializeNativeArrays();
            Reset();
        }

        private void InitializeNativeArrays()
        {
            // Initialize with reasonable default sizes - will be resized as needed
            nativeAudioBuffer = new NativeArray<float>(4096, Allocator.Persistent);
            // Expanded shared state: [0]=currentGain, [1]=peakLevel, [2]=gateSmoothing, 
            // [3-4]=highPassInputs, [5-6]=highPassOutputs, [7-8]=lowPassInputs, [9-10]=lowPassOutputs, 
            // [11]=lastSpeechTime, [12]=gateIsOpen, [13]=dcBlockInput, [14]=dcBlockOutput
            nativeSharedState = new NativeArray<float>(16, Allocator.Persistent);
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

            // Reset filter states
            for (int i = 0; i < 2; i++)
            {
                highPassPrevInputs[i] = 0f;
                highPassPrevOutputs[i] = 0f;
                lowPassPrevInputs[i] = 0f;
                lowPassPrevOutputs[i] = 0f;
            }
            
            // Reset DC blocking filter
            dcBlockPrevInput = 0f;
            dcBlockPrevOutput = 0f;
            
            CurrentGain = 1f;
            peakLevel = 0f;
            GateSmoothing = 0f;
            AdaptiveThreshold = configuration.MicrophoneLoudnessMinimumThreshold;
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

            // Update shared state with filter states
            nativeSharedState[0] = CurrentGain;
            nativeSharedState[1] = peakLevel;
            nativeSharedState[2] = GateSmoothing;
            
            // Store filter states (up to 2 stages each)
            for (int i = 0; i < 2; i++)
            {
                nativeSharedState[3 + i] = i < highPassPrevInputs.Length ? highPassPrevInputs[i] : 0f;
                nativeSharedState[5 + i] = i < highPassPrevOutputs.Length ? highPassPrevOutputs[i] : 0f;
                nativeSharedState[7 + i] = i < lowPassPrevInputs.Length ? lowPassPrevInputs[i] : 0f;
                nativeSharedState[9 + i] = i < lowPassPrevOutputs.Length ? lowPassPrevOutputs[i] : 0f;
            }
            
            nativeSharedState[11] = lastSpeechTime;
            nativeSharedState[12] = gateIsOpen ? 1f : 0f;
            nativeSharedState[13] = dcBlockPrevInput;
            nativeSharedState[14] = dcBlockPrevOutput;

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
                    enableBandPassFilter = configuration.EnableBandPassFilter,
                    enableNoiseGate = configuration.EnableNoiseGate,
                    enableAutoGainControl = configuration.EnableAutoGainControl,
                    highPassCutoffFreq = configuration.HighPassCutoffFreq,
                    lowPassCutoffFreq = configuration.LowPassCutoffFreq,
                    noiseGateThreshold = configuration.NoiseGateThreshold,
                    noiseGateHoldTime = configuration.NoiseGateHoldTime,
                    noiseGateAttackTime = configuration.NoiseGateAttackTime,
                    noiseGateReleaseTime = configuration.NoiseGateReleaseTime,
                    agcTargetLevel = configuration.AGCTargetLevel,
                    agcResponseSpeed = configuration.AGCResponseSpeed,
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
            
            // Restore filter states
            for (int i = 0; i < highPassPrevInputs.Length && i < 2; i++)
            {
                highPassPrevInputs[i] = nativeSharedState[3 + i];
                highPassPrevOutputs[i] = nativeSharedState[5 + i];
                lowPassPrevInputs[i] = nativeSharedState[7 + i];
                lowPassPrevOutputs[i] = nativeSharedState[9 + i];
            }
            
            lastSpeechTime = nativeSharedState[11];
            gateIsOpen = nativeSharedState[12] > 0.5f;
            dcBlockPrevInput = nativeSharedState[13];
            dcBlockPrevOutput = nativeSharedState[14];
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

                // Apply band-pass filter to isolate human voice frequencies
                if (configuration.EnableBandPassFilter) { sample = ApplyBandPassFilter(sample, sampleRate); }

                // Apply noise gate with hold time
                if (configuration.EnableNoiseGate) { sample = ApplyNoiseGateWithHold(sample, deltaTime); }

                // Apply automatic gain control
                if (configuration.EnableAutoGainControl) { sample = ApplyAGC(sample); }

                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }

        private float ApplyBandPassFilter(float input, int sampleRate)
        {
            // Apply DC blocking filter first to prevent offset accumulation
            float sample = ApplyDCBlockingFilter(input, sampleRate);
            
            // Apply 2nd order high-pass filter to remove low-frequency noise
            sample = ApplyHighPassFilter2ndOrder(sample, sampleRate);
            
            // Apply 2nd order low-pass filter to remove high-frequency noise
            sample = ApplyLowPassFilter2ndOrder(sample, sampleRate);
            
            // Apply de-esser to reduce harsh sibilants (configurable)
            if (configuration.EnableDeEsser && configuration.LowPassCutoffFreq > 4000f) // Only apply if we're not already filtering out high frequencies
            {
                sample = ApplyDeEsser(sample, sampleRate);
            }
            
            return sample;
        }

        private float ApplyDCBlockingFilter(float input, int sampleRate)
        {
            // Improved DC blocking filter with better stability
            float rc = 1f / (2f * Mathf.PI * 20f); // 20Hz cutoff
            float dt = 1f / sampleRate;
            float alpha = rc / (rc + dt);
            
            // Clamp alpha to prevent instability
            alpha = Mathf.Clamp(alpha, 0.9f, 0.999f);

            float output = alpha * (dcBlockPrevOutput + input - dcBlockPrevInput);

            // Denormal protection
            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            dcBlockPrevInput = input;
            dcBlockPrevOutput = output;

            return output;
        }

        /// <summary>
        /// Simple de-esser to reduce harsh sibilant sounds (S, T, SH sounds)
        /// </summary>
        private float ApplyDeEsser(float input, int sampleRate)
        {
            // Simple high-frequency detection and compression
            // This targets the 4-8kHz range where sibilants are most prominent
            
            // High-pass filter to isolate sibilant frequencies
            float sibilantFreq = input; // Simplified - in practice would use a proper high-pass at ~4kHz
            
            // Detect sibilant energy
            float sibilantLevel = Mathf.Abs(sibilantFreq);
            
            // Apply gentle compression if sibilant level is high
            float threshold = configuration.DeEsserThreshold;
            float ratio = configuration.DeEsserRatio;
            
            if (sibilantLevel > threshold)
            {
                float excess = sibilantLevel - threshold;
                float compressedExcess = excess / ratio;
                float reduction = excess - compressedExcess;
                
                // Apply reduction proportionally to the input
                float reductionFactor = 1f - (reduction / sibilantLevel) * 0.5f; // 50% max reduction
                return input * reductionFactor;
            }
            
            return input;
        }

        private float ApplyHighPassFilter2ndOrder(float input, int sampleRate)
        {
            // 2nd order Butterworth high-pass filter with stability improvements
            float w = 2f * Mathf.PI * configuration.HighPassCutoffFreq / sampleRate;
            
            // Clamp frequency to prevent instability
            w = Mathf.Clamp(w, 0.01f, Mathf.PI * 0.95f);
            
            float cosw = Mathf.Cos(w);
            float sinw = Mathf.Sin(w);
            float alpha = sinw / (2f * 0.7071f); // Q = 0.7071 for Butterworth
            
            float b0 = (1f + cosw) / 2f;
            float b1 = -(1f + cosw);
            float b2 = (1f + cosw) / 2f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosw;
            float a2 = 1f - alpha;

            // Normalize coefficients
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            // Apply biquad filter with denormal protection
            float output = b0 * input + b1 * highPassPrevInputs[0] + b2 * highPassPrevInputs[1] 
                         - a1 * highPassPrevOutputs[0] - a2 * highPassPrevOutputs[1];

            // Denormal protection - prevent very small values from causing CPU spikes
            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            // Shift delay line
            highPassPrevInputs[1] = highPassPrevInputs[0];
            highPassPrevInputs[0] = input;
            highPassPrevOutputs[1] = highPassPrevOutputs[0];
            highPassPrevOutputs[0] = output;

            return output;
        }

        private float ApplyLowPassFilter2ndOrder(float input, int sampleRate)
        {
            // 2nd order Butterworth low-pass filter with stability improvements
            float w = 2f * Mathf.PI * configuration.LowPassCutoffFreq / sampleRate;
            
            // Clamp frequency to prevent instability
            w = Mathf.Clamp(w, 0.01f, Mathf.PI * 0.95f);
            
            float cosw = Mathf.Cos(w);
            float sinw = Mathf.Sin(w);
            float alpha = sinw / (2f * 0.7071f); // Q = 0.7071 for Butterworth
            
            float b0 = (1f - cosw) / 2f;
            float b1 = 1f - cosw;
            float b2 = (1f - cosw) / 2f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosw;
            float a2 = 1f - alpha;

            // Normalize coefficients
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            // Apply biquad filter with denormal protection
            float output = b0 * input + b1 * lowPassPrevInputs[0] + b2 * lowPassPrevInputs[1] 
                         - a1 * lowPassPrevOutputs[0] - a2 * lowPassPrevOutputs[1];

            // Denormal protection - prevent very small values from causing CPU spikes
            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            // Shift delay line
            lowPassPrevInputs[1] = lowPassPrevInputs[0];
            lowPassPrevInputs[0] = input;
            lowPassPrevOutputs[1] = lowPassPrevOutputs[0];
            lowPassPrevOutputs[0] = output;

            return output;
        }

        private float ApplyNoiseGateWithHold(float sample, float deltaTime)
        {
            float sampleAbs = Mathf.Abs(sample);

            // Use the lower of the two thresholds to be more permissive for speech
            float effectiveThreshold = Mathf.Min(configuration.NoiseGateThreshold, AdaptiveThreshold * 0.3f);

            // But ensure we don't go below the configured threshold
            effectiveThreshold = Mathf.Max(effectiveThreshold, configuration.NoiseGateThreshold * 0.5f);

            bool speechDetected = sampleAbs > effectiveThreshold;

            if (speechDetected)
            {
                lastSpeechTime = 0f;
                gateIsOpen = true;
            }
            else
                lastSpeechTime += deltaTime;

            bool shouldGateBeOpen = speechDetected || (gateIsOpen && lastSpeechTime < configuration.NoiseGateHoldTime);

            // Detect gate state changes
            bool gateStateChanged = gateIsOpen != shouldGateBeOpen;
            gateIsOpen = shouldGateBeOpen;

            // Calculate target gate value
            float targetGate = gateIsOpen ? 1f : 0f;

            // Apply attack/release timing with improved smoothing
            float gateSpeed;

            if (targetGate > GateSmoothing)
            {
                // Opening gate (attack) - use exponential curve for smoother opening
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateAttackTime * 100f, 10f); // Minimum speed limit
            }
            else
            {
                // Closing gate (release) - use logarithmic curve for natural decay
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateReleaseTime * 50f, 5f); // Minimum speed limit
            }

            // Use exponential smoothing for more natural transitions
            float smoothingFactor = Mathf.Clamp01(gateSpeed);
            GateSmoothing = GateSmoothing + (targetGate - GateSmoothing) * smoothingFactor;

            // Apply additional smoothing to prevent clicks
            GateSmoothing = Mathf.Clamp01(GateSmoothing);

            // Reset filter states when gate fully closes to prevent state accumulation
            if (!gateIsOpen && GateSmoothing < 0.01f)
            {
                ResetFilterStates();
            }

            // Apply soft knee compression near the gate threshold to reduce harshness
            float gateMultiplier = GateSmoothing;
            if (GateSmoothing > 0.1f && GateSmoothing < 0.9f)
            {
                // Apply soft knee - smooth transition zone
                float knee = 0.3f; // Knee width
                float ratio = (GateSmoothing - 0.1f) / 0.8f; // Normalize to 0-1
                gateMultiplier = 0.1f + 0.8f * (ratio * ratio * (3f - 2f * ratio)); // Smooth step function
            }

            return sample * gateMultiplier;
        }

        /// <summary>
        ///Reset filter states to prevent accumulation of artifacts during silence
        /// </summary>
        private void ResetFilterStates()
        {
            for (int i = 0; i < 2; i++)
            {
                // Only reset if values are very small to avoid audible clicks during speech
                if (Mathf.Abs(highPassPrevInputs[i]) < 0.001f && Mathf.Abs(highPassPrevOutputs[i]) < 0.001f)
                {
                    highPassPrevInputs[i] = 0f;
                    highPassPrevOutputs[i] = 0f;
                }
                if (Mathf.Abs(lowPassPrevInputs[i]) < 0.001f && Mathf.Abs(lowPassPrevOutputs[i]) < 0.001f)
                {
                    lowPassPrevInputs[i] = 0f;
                    lowPassPrevOutputs[i] = 0f;
                }
            }
            
            // Reset DC blocking filter if values are small
            if (Mathf.Abs(dcBlockPrevInput) < 0.001f && Mathf.Abs(dcBlockPrevOutput) < 0.001f)
            {
                dcBlockPrevInput = 0f;
                dcBlockPrevOutput = 0f;
            }
        }

        private float ApplyAGC(float sample)
        {
            float sampleAbs = Mathf.Abs(sample);

            // Update peak level with improved decay and attack characteristics
            var peakDecay = 0.9995f; // Slower decay for more stable AGC
            var peakAttack = 0.1f; // Fast attack for transients
            
            if (sampleAbs > peakLevel)
            {
                // Fast attack for peaks
                peakLevel = Mathf.Lerp(peakLevel, sampleAbs, peakAttack);
            }
            else
            {
                // Slow decay for sustained levels
                peakLevel = peakLevel * peakDecay;
            }

            if (peakLevel > 0.001f) // Avoid division by zero
            {
                float targetGain = configuration.AGCTargetLevel / peakLevel;
                
                // More conservative gain limits to prevent artifacts
                targetGain = Mathf.Clamp(targetGain, 0.2f, 3f); // Reduced max gain

                // Adaptive gain speed based on gain change magnitude
                float gainDifference = Mathf.Abs(targetGain - CurrentGain);
                float baseSpeed = configuration.AGCResponseSpeed * 0.002f; // Slower base speed
                
                // Slower adjustment for large gain changes to prevent pumping
                float adaptiveSpeed = gainDifference > 0.5f ? baseSpeed * 0.3f : baseSpeed;
                
                // Apply lookahead smoothing to prevent rapid gain changes
                float smoothingWindow = 0.95f; // Smoothing factor
                CurrentGain = CurrentGain * smoothingWindow + targetGain * (1f - smoothingWindow) * adaptiveSpeed;
                
                // Additional limiting to prevent extreme gain changes
                CurrentGain = Mathf.Clamp(CurrentGain, 0.2f, 3f);
            }

            // Apply soft limiting to prevent clipping
            float processedSample = sample * CurrentGain;
            
            // Soft limiter with smooth knee
            if (Mathf.Abs(processedSample) > 0.95f)
            {
                float sign = Mathf.Sign(processedSample);
                float magnitude = Mathf.Abs(processedSample);
                
                // Soft knee compression above 0.95
                float compressedMagnitude = 0.95f + (magnitude - 0.95f) * 0.1f; // 10:1 ratio above threshold
                processedSample = sign * Mathf.Min(compressedMagnitude, 0.99f); // Hard limit at 0.99
            }

            return processedSample;
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

        [ReadOnly] public bool enableBandPassFilter;
        [ReadOnly] public bool enableNoiseGate;
        [ReadOnly] public bool enableAutoGainControl;
        [ReadOnly] public float highPassCutoffFreq;
        [ReadOnly] public float lowPassCutoffFreq;
        [ReadOnly] public float noiseGateThreshold;
        [ReadOnly] public float noiseGateHoldTime;
        [ReadOnly] public float noiseGateAttackTime;
        [ReadOnly] public float noiseGateReleaseTime;
        [ReadOnly] public float agcTargetLevel;
        [ReadOnly] public float agcResponseSpeed;
        [ReadOnly] public int sampleRate;

        // Shared state - [0]=currentGain, [1]=peakLevel, [2]=gateSmoothing, [3-4]=highPassInputs, [5-6]=highPassOutputs, [7-8]=lowPassInputs, [9-10]=lowPassOutputs, [11]=lastSpeechTime, [12]=gateIsOpen, [13]=dcBlockInput, [14]=dcBlockOutput
        public NativeArray<float> sharedState;

        [ReadOnly] public float adaptiveThreshold;

        public void Execute()
        {
            // Load filter state
            float currentGain = sharedState[0];
            float peakLevel = sharedState[1];
            float gateSmoothing = sharedState[2];
            float lastSpeechTime = sharedState[11];
            bool gateIsOpen = sharedState[12] > 0.5f;

            // Calculate delta time for this block
            float deltaTime = (float)audioData.Length / sampleRate;

            // Process each sample sequentially to maintain filter state
            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                if (enableBandPassFilter) { sample = ApplyBandPassFilterBurst(sample); }

                if (enableNoiseGate) { sample = ApplyNoiseGateBurst(sample, ref gateSmoothing, ref lastSpeechTime, ref gateIsOpen, deltaTime); }

                if (enableAutoGainControl) { sample = ApplyAGCBurst(sample, ref currentGain, ref peakLevel); }

                audioData[i] = math.clamp(sample, -1f, 1f);
            }

            // Save filter state back
            sharedState[0] = currentGain;
            sharedState[1] = peakLevel;
            sharedState[2] = gateSmoothing;
            sharedState[11] = lastSpeechTime;
            sharedState[12] = gateIsOpen ? 1f : 0f;
        }

        private float ApplyBandPassFilterBurst(float input)
        {
            // Apply DC blocking filter first
            float sample = ApplyDCBlockingFilterBurst(input);
            
            // Apply 2nd order high-pass filter
            sample = ApplyHighPassFilter2ndOrderBurst(sample);
            
            // Apply 2nd order low-pass filter
            sample = ApplyLowPassFilter2ndOrderBurst(sample);
            
            return sample;
        }

        private float ApplyDCBlockingFilterBurst(float input)
        {
            // Improved DC blocking filter with better stability
            float rc = 1f / (2f * math.PI * 20f); // 20Hz cutoff
            float dt = 1f / sampleRate;
            float alpha = rc / (rc + dt);
            
            // Clamp alpha to prevent instability
            alpha = math.clamp(alpha, 0.9f, 0.999f);

            float prevInput = sharedState[13];
            float prevOutput = sharedState[14];
            
            float output = alpha * (prevOutput + input - prevInput);

            // Denormal protection
            if (math.abs(output) < 1e-10f) output = 0f;

            sharedState[13] = input;
            sharedState[14] = output;

            return output;
        }

        private float ApplyHighPassFilter2ndOrderBurst(float input)
        {
            // 2nd order Butterworth high-pass filter with stability improvements
            float w = 2f * math.PI * highPassCutoffFreq / sampleRate;
            
            // Clamp frequency to prevent instability
            w = math.clamp(w, 0.01f, math.PI * 0.95f);
            
            float cosw = math.cos(w);
            float sinw = math.sin(w);
            float alpha = sinw / (2f * 0.7071f); // Q = 0.7071 for Butterworth
            
            float b0 = (1f + cosw) / 2f;
            float b1 = -(1f + cosw);
            float b2 = (1f + cosw) / 2f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosw;
            float a2 = 1f - alpha;

            // Normalize coefficients
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            // Apply biquad filter with denormal protection
            float output = b0 * input + b1 * sharedState[3] + b2 * sharedState[4] 
                         - a1 * sharedState[5] - a2 * sharedState[6];

            // Denormal protection - prevent very small values from causing CPU spikes
            if (math.abs(output) < 1e-10f) output = 0f;

            // Shift delay line
            sharedState[4] = sharedState[3];
            sharedState[3] = input;
            sharedState[6] = sharedState[5];
            sharedState[5] = output;

            return output;
        }

        private float ApplyLowPassFilter2ndOrderBurst(float input)
        {
            // 2nd order Butterworth low-pass filter with stability improvements
            float w = 2f * math.PI * lowPassCutoffFreq / sampleRate;
            
            // Clamp frequency to prevent instability
            w = math.clamp(w, 0.01f, math.PI * 0.95f);
            
            float cosw = math.cos(w);
            float sinw = math.sin(w);
            float alpha = sinw / (2f * 0.7071f); // Q = 0.7071 for Butterworth
            
            float b0 = (1f - cosw) / 2f;
            float b1 = 1f - cosw;
            float b2 = (1f - cosw) / 2f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosw;
            float a2 = 1f - alpha;

            // Normalize coefficients
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            // Apply biquad filter with denormal protection
            float output = b0 * input + b1 * sharedState[7] + b2 * sharedState[8] 
                         - a1 * sharedState[9] - a2 * sharedState[10];

            // Denormal protection - prevent very small values from causing CPU spikes
            if (math.abs(output) < 1e-10f) output = 0f;

            // Shift delay line
            sharedState[8] = sharedState[7];
            sharedState[7] = input;
            sharedState[10] = sharedState[9];
            sharedState[9] = output;

            return output;
        }

        private float ApplyNoiseGateBurst(float sample, ref float gateSmoothing, ref float lastSpeechTime, ref bool gateIsOpen, float deltaTime)
        {
            float sampleAbs = math.abs(sample);

            float effectiveThreshold = math.max(noiseGateThreshold * 0.5f,
                                              math.min(noiseGateThreshold, adaptiveThreshold * 0.3f));

            bool speechDetected = sampleAbs > effectiveThreshold;

            if (speechDetected)
            {
                lastSpeechTime = 0f;
                gateIsOpen = true;
            }
            else
            {
                lastSpeechTime += deltaTime;
            }

            bool shouldGateBeOpen = speechDetected || (gateIsOpen && lastSpeechTime < noiseGateHoldTime);

            gateIsOpen = shouldGateBeOpen;

            float targetGate = gateIsOpen ? 1f : 0f;

            // Apply improved attack/release timing with minimum speed limits
            float gateSpeed;
            if (targetGate > gateSmoothing)
            {
                gateSpeed = 1f / math.max(noiseGateAttackTime * 100f, 10f); // Minimum speed limit
            }
            else
            {
                gateSpeed = 1f / math.max(noiseGateReleaseTime * 50f, 5f); // Minimum speed limit
            }

            // Use exponential smoothing for more natural transitions
            float smoothingFactor = math.clamp(gateSpeed, 0f, 1f);
            gateSmoothing = gateSmoothing + (targetGate - gateSmoothing) * smoothingFactor;
            gateSmoothing = math.clamp(gateSmoothing, 0f, 1f);

            // Reset filter states when gate fully closes to prevent state accumulation
            if (!gateIsOpen && gateSmoothing < 0.01f)
            {
                ResetFilterStatesBurst();
            }

            // Apply soft knee compression near the gate threshold
            float gateMultiplier = gateSmoothing;
            if (gateSmoothing > 0.1f && gateSmoothing < 0.9f)
            {
                // Apply soft knee - smooth transition zone
                float ratio = (gateSmoothing - 0.1f) / 0.8f; // Normalize to 0-1
                gateMultiplier = 0.1f + 0.8f * (ratio * ratio * (3f - 2f * ratio)); // Smooth step function
            }

            return sample * gateMultiplier;
        }

        private void ResetFilterStatesBurst()
        {
            // Reset filter states if they're very small to prevent accumulation
            for (int i = 0; i < 2; i++)
            {
                if (math.abs(sharedState[3 + i]) < 0.001f && math.abs(sharedState[5 + i]) < 0.001f)
                {
                    sharedState[3 + i] = 0f; // highPassPrevInputs
                    sharedState[5 + i] = 0f; // highPassPrevOutputs
                }
                if (math.abs(sharedState[7 + i]) < 0.001f && math.abs(sharedState[9 + i]) < 0.001f)
                {
                    sharedState[7 + i] = 0f; // lowPassPrevInputs
                    sharedState[9 + i] = 0f; // lowPassPrevOutputs
                }
            }
            
            // Reset DC blocking filter if values are small
            if (math.abs(sharedState[13]) < 0.001f && math.abs(sharedState[14]) < 0.001f)
            {
                sharedState[13] = 0f; // dcBlockPrevInput
                sharedState[14] = 0f; // dcBlockPrevOutput
            }
        }

        private float ApplyAGCBurst(float sample, ref float currentGain, ref float peakLevel)
        {
            float sampleAbs = math.abs(sample);

            // Update peak level with improved decay and attack characteristics
            var peakDecay = 0.9995f; // Slower decay for more stable AGC
            var peakAttack = 0.1f; // Fast attack for transients
            
            if (sampleAbs > peakLevel)
            {
                // Fast attack for peaks
                peakLevel = math.lerp(peakLevel, sampleAbs, peakAttack);
            }
            else
            {
                // Slow decay for sustained levels
                peakLevel = peakLevel * peakDecay;
            }

            if (peakLevel > 0.001f)
            {
                float targetGain = agcTargetLevel / peakLevel;
                
                // More conservative gain limits to prevent artifacts
                targetGain = math.clamp(targetGain, 0.2f, 3f); // Reduced max gain

                // Adaptive gain speed based on gain change magnitude
                float gainDifference = math.abs(targetGain - currentGain);
                float baseSpeed = agcResponseSpeed * 0.002f; // Slower base speed
                
                // Slower adjustment for large gain changes to prevent pumping
                float adaptiveSpeed = gainDifference > 0.5f ? baseSpeed * 0.3f : baseSpeed;
                
                // Apply lookahead smoothing to prevent rapid gain changes
                float smoothingWindow = 0.95f; // Smoothing factor
                currentGain = currentGain * smoothingWindow + targetGain * (1f - smoothingWindow) * adaptiveSpeed;
                
                // Additional limiting to prevent extreme gain changes
                currentGain = math.clamp(currentGain, 0.2f, 3f);
            }

            // Apply soft limiting to prevent clipping
            float processedSample = sample * currentGain;
            
            // Soft limiter with smooth knee
            if (math.abs(processedSample) > 0.95f)
            {
                float sign = math.sign(processedSample);
                float magnitude = math.abs(processedSample);
                
                // Soft knee compression above 0.95
                float compressedMagnitude = 0.95f + (magnitude - 0.95f) * 0.1f; // 10:1 ratio above threshold
                processedSample = sign * math.min(compressedMagnitude, 0.99f); // Hard limit at 0.99
            }

            return processedSample;
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
