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

        private readonly float[] highPassPrevInputs = new float[2];
        private readonly float[] highPassPrevOutputs = new float[2];
        private readonly float[] lowPassPrevInputs = new float[2];
        private readonly float[] lowPassPrevOutputs = new float[2];

        private float dcBlockPrevInput;
        private float dcBlockPrevOutput;

        private float peakLevel;
        private bool gateIsOpen;
        private float lastSpeechTime;

        private float[] fadeInBuffer;
        private int fadeInBufferIndex = 0;
        private bool isGateOpening = false;
        private float gateOpenFadeProgress = 0f;

        private float noiseFloor = 0f;
        private float noiseFloorUpdateTime = 0f;

        // Logging variables
        private float lastLogTime = 0f;
        private const float LOG_INTERVAL = 3f; // Log every 3 seconds
        private int processedBufferCount = 0;

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

        /// <summary>
        ///     Get comprehensive audio processing diagnostic information
        /// </summary>
        public string GetAudioDiagnostics(int sampleRate)
        {
            return $"Audio Processing Diagnostics:\n" +
                   $"Sample Rate: {sampleRate}Hz\n" +
                   $"High-Pass Cutoff: {configuration.HighPassCutoffFreq}Hz\n" +
                   $"Low-Pass Cutoff: {configuration.LowPassCutoffFreq}Hz\n" +
                   $"AGC Target Level: {configuration.AGCTargetLevel:F2}\n" +
                   $"Current Gain: {CurrentGain:F2}x\n" +
                   $"Noise Gate Threshold: {configuration.NoiseGateThreshold:F4}\n" +
                   $"Processing Enabled - BandPass: {configuration.EnableBandPassFilter}, " +
                   $"AGC: {configuration.EnableAutoGainControl}, " +
                   $"NoiseGate: {configuration.EnableNoiseGate}, " +
                   $"NoiseReduction: {configuration.EnableNoiseReduction}\n" +
                   $"POTENTIAL ISSUES:\n" +
                   $"- Low-pass too low (voice needs up to 8kHz): {(configuration.LowPassCutoffFreq < 8000 ? "YES" : "NO")}\n" +
                   $"- High AGC gain (may cause distortion): {(CurrentGain > 3.0f ? "YES" : "NO")}\n" +
                   $"- Noise gate too aggressive: {(configuration.NoiseGateThreshold > 0.01f ? "YES" : "NO")}";
        }

        public VoiceChatAudioProcessor(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;
            Reset();
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

            noiseFloor = 0f;
            noiseFloorUpdateTime = 0f;
        }

        /// <summary>
        ///     Process audio samples with noise reduction and other effects
        /// </summary>
        public void ProcessAudio(float[] audioData, int sampleRate)
        {
            if (audioData == null || audioData.Length == 0) return;

            float deltaTime = (float)audioData.Length / sampleRate;
            processedBufferCount++;

            // Calculate RMS for input level monitoring
            float inputRMS = CalculateRMS(audioData);

            // Periodic detailed logging
            bool shouldLogDetails = UnityEngine.Time.realtimeSinceStartup - lastLogTime > LOG_INTERVAL;
            if (shouldLogDetails)
            {
                UnityEngine.Debug.Log($"VoiceChatAudioProcessor: Processing buffer #{processedBufferCount}, " +
                    $"Length: {audioData.Length} samples, SampleRate: {sampleRate}Hz, " +
                    $"DeltaTime: {deltaTime:F4}s, InputRMS: {inputRMS:F4}");

                UnityEngine.Debug.Log($"Audio Processing Config: " +
                    $"BandPass={configuration.EnableBandPassFilter}, " +
                    $"NoiseReduction={configuration.EnableNoiseReduction}, " +
                    $"NoiseGate={configuration.EnableNoiseGate}, " +
                    $"AGC={configuration.EnableAutoGainControl}");

                lastLogTime = UnityEngine.Time.realtimeSinceStartup;
            }

            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];
                float originalSample = sample;

                if (configuration.EnableBandPassFilter)
                {
                    sample = ApplyBandPassFilter(sample, sampleRate);
                    if (shouldLogDetails && i == 0) // Log only first sample for efficiency
                    {
                        UnityEngine.Debug.Log($"BandPass Filter: {originalSample:F6} -> {sample:F6}");
                    }
                }

                if (configuration.EnableNoiseReduction)
                {
                    float preNR = sample;
                    sample = ApplyNoiseReduction(sample, deltaTime);
                    if (shouldLogDetails && i == 0)
                    {
                        UnityEngine.Debug.Log($"Noise Reduction: {preNR:F6} -> {sample:F6}");
                    }
                }

                if (configuration.EnableNoiseGate)
                {
                    float preGate = sample;
                    sample = ApplyNoiseGateWithHold(sample, deltaTime, sampleRate);
                    if (shouldLogDetails && i == 0)
                    {
                        UnityEngine.Debug.Log($"Noise Gate: {preGate:F6} -> {sample:F6}, Gate State: {GetDebugInfo()}");
                    }
                }

                if (configuration.EnableAutoGainControl)
                {
                    float preAGC = sample;
                    sample = ApplyAGC(sample);
                    if (shouldLogDetails && i == 0)
                    {
                        UnityEngine.Debug.Log($"AGC: {preAGC:F6} -> {sample:F6}, Gain: {CurrentGain:F3}x");
                    }
                }

                audioData[i] = UnityEngine.Mathf.Clamp(sample, -1f, 1f);
            }

            // Calculate and log output RMS
            if (shouldLogDetails)
            {
                float outputRMS = CalculateRMS(audioData);
                UnityEngine.Debug.Log($"Audio Processing Complete: InputRMS={inputRMS:F4} -> OutputRMS={outputRMS:F4}, " +
                    $"RMS Change: {(outputRMS / Mathf.Max(inputRMS, 1e-10f)):F3}x");
            }
        }

        private float CalculateRMS(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }
            return UnityEngine.Mathf.Sqrt(sum / samples.Length);
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
            float rc = 1f / (2f * Mathf.PI * 20f);
            float dt = 1f / sampleRate;
            float alpha = rc / (rc + dt);

            alpha = Mathf.Clamp(alpha, 0.9f, 0.999f);

            float output = alpha * (dcBlockPrevOutput + input - dcBlockPrevInput);

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            dcBlockPrevInput = input;
            dcBlockPrevOutput = output;

            return output;
        }

        private float ApplyHighPassFilter2ndOrder(float input, int sampleRate)
        {
            float w = 2f * Mathf.PI * configuration.HighPassCutoffFreq / sampleRate;
            w = Mathf.Clamp(w, 0.01f, Mathf.PI * 0.95f);

            float cosw = Mathf.Cos(w);
            float sinw = Mathf.Sin(w);
            float alpha = sinw / (2f * 0.7071f);

            float b0 = (1f + cosw) / 2f;
            float b1 = -(1f + cosw);
            float b2 = (1f + cosw) / 2f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosw;
            float a2 = 1f - alpha;

            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            float output = b0 * input + b1 * highPassPrevInputs[0] + b2 * highPassPrevInputs[1]
                         - a1 * highPassPrevOutputs[0] - a2 * highPassPrevOutputs[1];

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            highPassPrevInputs[1] = highPassPrevInputs[0];
            highPassPrevInputs[0] = input;
            highPassPrevOutputs[1] = highPassPrevOutputs[0];
            highPassPrevOutputs[0] = output;

            return output;
        }

        private float ApplyLowPassFilter2ndOrder(float input, int sampleRate)
        {
            float w = 2f * Mathf.PI * configuration.LowPassCutoffFreq / sampleRate;
            w = Mathf.Clamp(w, 0.01f, Mathf.PI * 0.95f);

            float cosw = Mathf.Cos(w);
            float sinw = Mathf.Sin(w);
            float alpha = sinw / (2f * 0.7071f);

            float b0 = (1f - cosw) / 2f;
            float b1 = 1f - cosw;
            float b2 = (1f - cosw) / 2f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosw;
            float a2 = 1f - alpha;

            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            float output = b0 * input + b1 * lowPassPrevInputs[0] + b2 * lowPassPrevInputs[1]
                         - a1 * lowPassPrevOutputs[0] - a2 * lowPassPrevOutputs[1];

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            lowPassPrevInputs[1] = lowPassPrevInputs[0];
            lowPassPrevInputs[0] = input;
            lowPassPrevOutputs[1] = lowPassPrevOutputs[0];
            lowPassPrevOutputs[0] = output;

            return output;
        }

        private float ApplyNoiseGateWithHold(float sample, float deltaTime, int sampleRate)
        {
            // Store the sample in fade-in buffer for gate opening fade
            if (fadeInBuffer != null && fadeInBuffer.Length > 0)
            {
                fadeInBuffer[fadeInBufferIndex] = sample;
                fadeInBufferIndex = (fadeInBufferIndex + 1) % fadeInBuffer.Length;
            }

            float sampleAbs = Mathf.Abs(sample);
            bool speechDetected = sampleAbs > configuration.NoiseGateThreshold;

            if (speechDetected)
            {
                lastSpeechTime = 0f;

                if (!gateIsOpen)
                {
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

            if (gateIsOpen && !shouldGateBeOpen)
            {
                gateIsOpen = false;
                isGateOpening = false;
                gateOpenFadeProgress = 0f;
            }

            float targetGate = gateIsOpen ? 1f : 0f;

            float gateSpeed;

            if (targetGate > GateSmoothing)
            {
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateAttackTime * 100f, 10f);
            }
            else
            {
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateReleaseTime * 50f, 5f);
            }

            float smoothingFactor = Mathf.Clamp01(gateSpeed);
            GateSmoothing = GateSmoothing + (targetGate - GateSmoothing) * smoothingFactor;
            GateSmoothing = Mathf.Clamp01(GateSmoothing);

            float processedSample = sample;

            if (configuration.EnableGateFadeIn && isGateOpening && gateOpenFadeProgress < 1f)
            {
                float fadeInSpeed = 1f / (configuration.NoiseGateAttackTime * sampleRate);
                gateOpenFadeProgress += fadeInSpeed;
                gateOpenFadeProgress = Mathf.Clamp01(gateOpenFadeProgress);

                float fadeCurve = gateOpenFadeProgress * gateOpenFadeProgress * (3f - 2f * gateOpenFadeProgress);

                int bufferLookback = Mathf.Min(configuration.FadeInBufferSize / 2, fadeInBuffer.Length - 1);
                int lookbackIndex = (fadeInBufferIndex - bufferLookback + fadeInBuffer.Length) % fadeInBuffer.Length;
                float preGateSample = fadeInBuffer[lookbackIndex] * configuration.PreGateAttenuation;

                processedSample = Mathf.Lerp(preGateSample, sample, fadeCurve);

                if (gateOpenFadeProgress >= 1f)
                {
                    isGateOpening = false;
                }
            }

            if (!gateIsOpen && GateSmoothing < 0.01f)
            {
                ResetFilterStatesGradually();
            }

            float gateMultiplier = GateSmoothing;
            if (GateSmoothing > 0.1f && GateSmoothing < 0.9f)
            {
                float ratio = (GateSmoothing - 0.1f) / 0.8f;
                gateMultiplier = 0.1f + 0.8f * (ratio * ratio * (3f - 2f * ratio));
            }

            return processedSample * gateMultiplier;
        }

        private void ResetFilterStates()
        {
            for (int i = 0; i < 2; i++)
            {
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

            if (Mathf.Abs(dcBlockPrevInput) < 0.001f && Mathf.Abs(dcBlockPrevOutput) < 0.001f)
            {
                dcBlockPrevInput = 0f;
                dcBlockPrevOutput = 0f;
            }
        }

        private void ResetFilterStatesGradually()
        {
            // Gradually decay filter states to prevent pops and clicks
            // This is much gentler than abrupt reset and prevents artifacts
            float decayFactor = 0.95f;

            for (int i = 0; i < 2; i++)
            {
                highPassPrevInputs[i] *= decayFactor;
                highPassPrevOutputs[i] *= decayFactor;

                lowPassPrevInputs[i] *= decayFactor;
                lowPassPrevOutputs[i] *= decayFactor;

                if (Mathf.Abs(highPassPrevInputs[i]) < 1e-10f) highPassPrevInputs[i] = 0f;
                if (Mathf.Abs(highPassPrevOutputs[i]) < 1e-10f) highPassPrevOutputs[i] = 0f;
                if (Mathf.Abs(lowPassPrevInputs[i]) < 1e-10f) lowPassPrevInputs[i] = 0f;
                if (Mathf.Abs(lowPassPrevOutputs[i]) < 1e-10f) lowPassPrevOutputs[i] = 0f;
            }

            dcBlockPrevInput *= decayFactor;
            dcBlockPrevOutput *= decayFactor;

            if (Mathf.Abs(dcBlockPrevInput) < 1e-10f) dcBlockPrevInput = 0f;
            if (Mathf.Abs(dcBlockPrevOutput) < 1e-10f) dcBlockPrevOutput = 0f;
        }

        private float ApplyAGC(float sample)
        {
            float sampleAbs = Mathf.Abs(sample);

            var peakDecay = 0.999f;
            var peakAttack = 0.3f;

            if (sampleAbs > peakLevel)
            {
                peakLevel = Mathf.Lerp(peakLevel, sampleAbs, peakAttack);
            }
            else
            {
                peakLevel = peakLevel * peakDecay;
            }

            if (peakLevel > 0.001f)
            {
                float targetGain = configuration.AGCTargetLevel / peakLevel;
                targetGain = Mathf.Clamp(targetGain, 0.1f, 5f);

                float gainDifference = Mathf.Abs(targetGain - CurrentGain);
                float baseSpeed = configuration.AGCResponseSpeed * 0.01f;

                float adaptiveSpeed = gainDifference > 1f ? baseSpeed * 0.8f : baseSpeed;

                CurrentGain = Mathf.Lerp(CurrentGain, targetGain, adaptiveSpeed);
                CurrentGain = Mathf.Clamp(CurrentGain, 0.1f, 5f);
            }

            float processedSample = sample * CurrentGain;

            if (Mathf.Abs(processedSample) > 0.95f)
            {
                float sign = Mathf.Sign(processedSample);
                float magnitude = Mathf.Abs(processedSample);

                float compressedMagnitude = 0.95f + (magnitude - 0.95f) * 0.1f;
                processedSample = sign * Mathf.Min(compressedMagnitude, 0.99f);
            }

            return processedSample;
        }

        private float ApplyNoiseReduction(float input, float deltaTime)
        {
            float inputAbs = Mathf.Abs(input);

            noiseFloorUpdateTime += deltaTime;

            if (inputAbs < configuration.NoiseGateThreshold)
            {
                if (noiseFloorUpdateTime > 0.1f)
                {
                    float targetNoiseFloor = inputAbs * 1.2f;
                    noiseFloor = Mathf.Lerp(noiseFloor, targetNoiseFloor, 0.01f);
                    noiseFloorUpdateTime = 0f;
                }
            }

            if (noiseFloor > 0.001f && inputAbs > noiseFloor)
            {
                float reductionStrength = configuration.NoiseReductionStrength;
                float noiseComponent = noiseFloor * reductionStrength;

                float sign = Mathf.Sign(input);
                float reducedMagnitude = Mathf.Max(0f, inputAbs - noiseComponent);

                return sign * reducedMagnitude;
            }

            return input;
        }
    }
}
