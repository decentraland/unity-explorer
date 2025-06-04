using UnityEngine;
using System;

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
        private int fadeInBufferIndex;
        private bool isGateOpening;
        private float gateOpenFadeProgress;

        private float noiseFloor;
        private float noiseFloorUpdateTime;
        
        private readonly object processingLock = new object();

        /// <summary>
        ///     Get the current gain level for UI feedback
        /// </summary>
        public float CurrentGain { get; private set; } = 1f;

        /// <summary>
        ///     Get the current gate smoothing value for debugging
        /// </summary>
        public float GateSmoothing { get; private set; }

        public VoiceChatAudioProcessor(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;
            Reset();
        }

        public void Reset()
        {
            lock (processingLock)
            {
            for (var i = 0; i < 2; i++)
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

            if (fadeInBuffer == null || fadeInBuffer.Length != configuration.FadeInBufferSize) { fadeInBuffer = new float[configuration.FadeInBufferSize]; }

            for (var i = 0; i < fadeInBuffer.Length; i++) { fadeInBuffer[i] = 0f; }

            fadeInBufferIndex = 0;
            isGateOpening = false;
            gateOpenFadeProgress = 0f;

            noiseFloor = 0f;
            noiseFloorUpdateTime = 0f;
            }
        }

        /// <summary>
        ///     Process audio samples with noise reduction and other effects
        ///     Thread-safe for real-time background processing
        /// </summary>
        public void ProcessAudio(float[] audioData, int sampleRate, int sampleCount = -1)
        {
            if (audioData == null || audioData.Length == 0) return;

            // Use provided sampleCount or default to array length
            int actualSampleCount = sampleCount > 0 ? sampleCount : audioData.Length;
            
            lock (processingLock)
            {
                float sampleDeltaTime = (float)actualSampleCount / sampleRate;

                for (var i = 0; i < actualSampleCount; i++)
            {
                float sample = audioData[i];

                if (configuration.EnableBandPassFilter) { sample = ApplyBandPassFilter(sample, sampleRate); }

                    if (configuration.EnableNoiseReduction) { sample = ApplyNoiseReduction(sample, sampleDeltaTime); }

                    if (configuration.EnableNoiseGate) { sample = ApplyNoiseGateWithHold(sample, sampleDeltaTime, sampleRate); }

                if (configuration.EnableAutoGainControl) { sample = ApplyAGC(sample); }

                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
                }
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

            b0 /= a0;
            b1 /= a0;
            b2 /= a0;
            a1 /= a0;
            a2 /= a0;

            float output = (b0 * input) + (b1 * highPassPrevInputs[0]) + (b2 * highPassPrevInputs[1])
                           - (a1 * highPassPrevOutputs[0]) - (a2 * highPassPrevOutputs[1]);

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

            b0 /= a0;
            b1 /= a0;
            b2 /= a0;
            a1 /= a0;
            a2 /= a0;

            float output = (b0 * input) + (b1 * lowPassPrevInputs[0]) + (b2 * lowPassPrevInputs[1])
                           - (a1 * lowPassPrevOutputs[0]) - (a2 * lowPassPrevOutputs[1]);

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            lowPassPrevInputs[1] = lowPassPrevInputs[0];
            lowPassPrevInputs[0] = input;
            lowPassPrevOutputs[1] = lowPassPrevOutputs[0];
            lowPassPrevOutputs[0] = output;

            return output;
        }

        private float ApplyNoiseGateWithHold(float sample, float deltaTime, int sampleRate)
        {
            float sampleAbs = Mathf.Abs(sample);

            fadeInBuffer[fadeInBufferIndex] = sample;
            fadeInBufferIndex = (fadeInBufferIndex + 1) % fadeInBuffer.Length;

            float effectiveThreshold = configuration.NoiseGateThreshold;

            bool speechDetected = sampleAbs > effectiveThreshold;

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
            else { lastSpeechTime += deltaTime; }

            bool shouldGateBeOpen = speechDetected || (gateIsOpen && lastSpeechTime < configuration.NoiseGateHoldTime);

            if (gateIsOpen && !shouldGateBeOpen)
            {
                gateIsOpen = false;
                isGateOpening = false;
                gateOpenFadeProgress = 0f;
            }

            float targetGate = gateIsOpen ? 1f : 0f;

            float gateSpeed;

            if (targetGate > GateSmoothing) { gateSpeed = 1f / Mathf.Max(configuration.NoiseGateAttackTime * 100f, 10f); }
            else { gateSpeed = 1f / Mathf.Max(configuration.NoiseGateReleaseTime * 50f, 5f); }

            float smoothingFactor = Mathf.Clamp01(gateSpeed);
            GateSmoothing += (targetGate - GateSmoothing) * smoothingFactor;
            GateSmoothing = Mathf.Clamp01(GateSmoothing);

            float processedSample = sample;

            if (configuration.EnableGateFadeIn && isGateOpening && gateOpenFadeProgress < 1f)
            {
                float fadeInSpeed = 1f / (configuration.NoiseGateAttackTime * sampleRate);
                gateOpenFadeProgress += fadeInSpeed;
                gateOpenFadeProgress = Mathf.Clamp01(gateOpenFadeProgress);

                float fadeCurve = gateOpenFadeProgress * gateOpenFadeProgress * (3f - (2f * gateOpenFadeProgress));

                int bufferLookback = Mathf.Min(configuration.FadeInBufferSize / 2, fadeInBuffer.Length - 1);
                int lookbackIndex = (fadeInBufferIndex - bufferLookback + fadeInBuffer.Length) % fadeInBuffer.Length;
                float preGateSample = fadeInBuffer[lookbackIndex] * configuration.PreGateAttenuation;

                processedSample = Mathf.Lerp(preGateSample, sample, fadeCurve);

                if (gateOpenFadeProgress >= 1f) { isGateOpening = false; }
            }

            if (!gateIsOpen && GateSmoothing < 0.01f) { ResetFilterStatesGradually(); }

            float gateMultiplier = GateSmoothing;

            if (GateSmoothing > 0.1f && GateSmoothing < 0.9f)
            {
                float ratio = (GateSmoothing - 0.1f) / 0.8f;
                gateMultiplier = 0.1f + (0.8f * (ratio * ratio * (3f - (2f * ratio))));
            }

            return processedSample * gateMultiplier;
        }

        private void ResetFilterStatesGradually()
        {
            // Gradually decay filter states to prevent pops and clicks
            // This is much gentler than abrupt reset and prevents artifacts
            var decayFactor = 0.95f;

            for (var i = 0; i < 2; i++)
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

            if (sampleAbs > peakLevel) { peakLevel = Mathf.Lerp(peakLevel, sampleAbs, peakAttack); }
            else { peakLevel = peakLevel * peakDecay; }

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

                float compressedMagnitude = 0.95f + ((magnitude - 0.95f) * 0.1f);
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
