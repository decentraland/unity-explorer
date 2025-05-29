using UnityEngine;

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

        // Fade-in buffer to eliminate gate opening pops
        private float[] fadeInBuffer;
        private int fadeInBufferIndex = 0;
        private bool isGateOpening = false;
        private float gateOpenFadeProgress = 0f;

        /// <summary>
        ///     Get the current noise gate status for UI feedback
        /// </summary>
        public bool IsGateOpen => GateSmoothing > 0.5f;

        /// <summary>
        ///     Get the current gain level for UI feedback
        /// </summary>
        public float CurrentGain { get; private set; } = 1f;

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
                   $"FadeIn: {(isGateOpening ? $"ACTIVE ({gateOpenFadeProgress:F2})" : "IDLE")}, " +
                   $"BufferSize: {(fadeInBuffer?.Length ?? 0)}/{configuration.FadeInBufferSize}";
        }

        /// <summary>
        ///     Get detailed fade-in configuration for debugging
        /// </summary>
        public string GetFadeInDebugInfo()
        {
            return $"FadeIn Enabled: {configuration.EnableGateFadeIn}, " +
                   $"Buffer Size: {configuration.FadeInBufferSize} (actual: {(fadeInBuffer?.Length ?? 0)}), " +
                   $"Pre-Gate Attenuation: {configuration.PreGateAttenuation:F3}, " +
                   $"Attack Time: {configuration.NoiseGateAttackTime:F3}s, " +
                   $"Is Opening: {isGateOpening}, " +
                   $"Progress: {gateOpenFadeProgress:F3}";
        }

        public VoiceChatAudioProcessor(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;
            Reset();
        }

        public void Dispose()
        {
            // No resources to dispose in the simplified implementation
        }

        public void Reset()
        {
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
            gateIsOpen = false;
            lastSpeechTime = 0f;

            if (fadeInBuffer == null || fadeInBuffer.Length != configuration.FadeInBufferSize)
            {
                fadeInBuffer = new float[configuration.FadeInBufferSize];
            }
            
            for (int i = 0; i < fadeInBuffer.Length; i++)
            {
                fadeInBuffer[i] = 0f;
            }
            fadeInBufferIndex = 0;
            isGateOpening = false;
            gateOpenFadeProgress = 0f;
        }

        /// <summary>
        ///     Process audio samples with noise reduction and other effects
        /// </summary>
        public void ProcessAudio(float[] audioData, int sampleRate)
        {
            if (audioData == null || audioData.Length == 0) return;

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
            float sample = ApplyDCBlockingFilter(input, sampleRate);
            sample = ApplyHighPassFilter2ndOrder(sample, sampleRate);
            sample = ApplyLowPassFilter2ndOrder(sample, sampleRate);
            
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

            // Always store samples in the fade-in buffer for potential crossfading
            fadeInBuffer[fadeInBufferIndex] = sample;
            fadeInBufferIndex = (fadeInBufferIndex + 1) % fadeInBuffer.Length;

            // Use the noise gate threshold for speech detection
            float effectiveThreshold = configuration.NoiseGateThreshold;

            bool speechDetected = sampleAbs > effectiveThreshold;
            bool wasGateOpen = gateIsOpen;

            if (speechDetected)
            {
                lastSpeechTime = 0f;
                if (!gateIsOpen)
                {
                    // Gate is opening - start fade-in process
                    gateIsOpen = true;
                    isGateOpening = true;
                    gateOpenFadeProgress = 0f;
                }
            }
            else
            {
                lastSpeechTime += deltaTime;
            }

            bool shouldGateBeOpen = speechDetected || (gateIsOpen && lastSpeechTime < configuration.NoiseGateHoldTime);

            // Handle gate closing
            if (gateIsOpen && !shouldGateBeOpen)
            {
                gateIsOpen = false;
                isGateOpening = false;
                gateOpenFadeProgress = 0f;
            }

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

            // Handle fade-in crossfading when gate opens
            float processedSample = sample;
            
            if (configuration.EnableGateFadeIn && isGateOpening && gateOpenFadeProgress < 1f)
            {
                // Calculate fade-in progress - use proper timing based on attack time
                float fadeInSpeed = 1f / (configuration.NoiseGateAttackTime * 48000f); // Samples per second
                gateOpenFadeProgress += fadeInSpeed; // Per sample increment
                gateOpenFadeProgress = Mathf.Clamp01(gateOpenFadeProgress);

                // Create smooth fade-in curve (S-curve for natural sound)
                float fadeCurve = gateOpenFadeProgress * gateOpenFadeProgress * (3f - 2f * gateOpenFadeProgress);

                // Get pre-gate sample from buffer for crossfading
                int bufferLookback = Mathf.Min(configuration.FadeInBufferSize / 2, fadeInBuffer.Length - 1); // Use half buffer for lookback
                int lookbackIndex = (fadeInBufferIndex - bufferLookback + fadeInBuffer.Length) % fadeInBuffer.Length;
                float preGateSample = fadeInBuffer[lookbackIndex] * configuration.PreGateAttenuation;

                // Crossfade between attenuated pre-gate audio and current sample
                processedSample = Mathf.Lerp(preGateSample, sample, fadeCurve);

                // Mark fade-in as complete
                if (gateOpenFadeProgress >= 1f)
                {
                    isGateOpening = false;
                }
            }

            // Reset filter states when gate fully closes to prevent state accumulation
            if (!gateIsOpen && GateSmoothing < 0.01f)
            {
                ResetFilterStates();
            }

            // Apply soft knee compression near the gate threshold to reduce harshness
            float gateMultiplier = GateSmoothing;
            if (GateSmoothing > 0.1f && GateSmoothing < 0.9f)
            {
                float ratio = (GateSmoothing - 0.1f) / 0.8f; 
                gateMultiplier = 0.1f + 0.8f * (ratio * ratio * (3f - 2f * ratio));
            }

            return processedSample * gateMultiplier;
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
}
