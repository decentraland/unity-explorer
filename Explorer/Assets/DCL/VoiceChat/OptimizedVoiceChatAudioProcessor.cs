using UnityEngine;
using System;

namespace DCL.VoiceChat
{
    // TODO: Review if this class will be needed, if so, we need to improve it by using Burst and potentially Jobs.
    // This would require further benchmarking to see if its worth.
    public class OptimizedVoiceChatAudioProcessor : IVoiceChatAudioProcessor
    {
        private readonly VoiceChatConfiguration configuration;
        private readonly float[] lowPassPrevInputs;
        private readonly float[] lowPassPrevOutputs;
        private readonly float[] fadeInBuffer;
        private readonly float[] tempBuffer;

        private float peakLevel;
        private bool gateIsOpen;
        private float lastSpeechTime;
        private int fadeInBufferIndex;
        private bool isGateOpening;
        private float gateOpenFadeProgress;
        private float gateSmoothing;
        private float targetGain;
        private float currentGain;
        private float peakTrackingTime;
        private float lastLowPassCutoff;
        private int lastLowPassSampleRate;
        private float gateSpeed;
        private float gateMultiplier;
        private float effectiveThreshold;
        private float fadeInSpeed;
        private float bufferLookback;
        private float preGateAttenuation;

        private struct BiquadState
        {
            public float x1, x2, y1, y2;
        }

        private struct BiquadCoefficients
        {
            public float b0, b1, b2, a1, a2;
        }

        private BiquadState lowPassState;
        private BiquadCoefficients lowPassCoeffs;

        public OptimizedVoiceChatAudioProcessor(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;

            lowPassPrevInputs = new float[2];
            lowPassPrevOutputs = new float[2];
            fadeInBuffer = new float[configuration.FadeInBufferSize];
            tempBuffer = new float[configuration.FadeInBufferSize];

            effectiveThreshold = configuration.NoiseGateThreshold;
            preGateAttenuation = configuration.PreGateAttenuation;
            bufferLookback = Mathf.Min(configuration.FadeInBufferSize / 2, fadeInBuffer.Length - 1);

            Reset();
        }


        public void Reset()
        {
            Array.Clear(lowPassPrevInputs, 0, lowPassPrevInputs.Length);
            Array.Clear(lowPassPrevOutputs, 0, lowPassPrevOutputs.Length);
            Array.Clear(fadeInBuffer, 0, fadeInBuffer.Length);
            Array.Clear(tempBuffer, 0, tempBuffer.Length);

            peakLevel = 0f;
            gateSmoothing = 0f;
            gateIsOpen = false;
            lastSpeechTime = 0f;
            currentGain = 1f;
            targetGain = 1f;
            peakTrackingTime = 0f;
            fadeInBufferIndex = 0;
            isGateOpening = false;
            gateOpenFadeProgress = 0f;
            lowPassState = default;
            lastLowPassCutoff = -1f;
            lastLowPassSampleRate = -1;
        }

        private void UpdateLowPassCoefficients(float cutoffFreq, int sampleRate)
        {
            if (Mathf.Approximately(cutoffFreq, lastLowPassCutoff) && sampleRate == lastLowPassSampleRate)
                return;

            float w = 2f * Mathf.PI * cutoffFreq / sampleRate;
            w = Mathf.Clamp(w, 0.01f, Mathf.PI * 0.95f);
            float cosw = Mathf.Cos(w);
            float sinw = Mathf.Sin(w);
            float alpha = sinw * 0.7071f;

            float a0 = 1f + alpha;
            float a0Inv = 1f / a0;
            float b0 = (1f - cosw) * 0.5f;
            float b1 = 1f - cosw;

            lowPassCoeffs.b0 = b0 * a0Inv;
            lowPassCoeffs.b1 = b1 * a0Inv;
            lowPassCoeffs.b2 = b0 * a0Inv;
            lowPassCoeffs.a1 = -2f * cosw * a0Inv;
            lowPassCoeffs.a2 = (1f - alpha) * a0Inv;

            lastLowPassCutoff = cutoffFreq;
            lastLowPassSampleRate = sampleRate;
        }

        private void UpdateAutoGain(float deltaTime, bool speechDetected)
        {
            if (speechDetected)
            {
                peakTrackingTime += deltaTime;

                if (peakTrackingTime >= configuration.PeakTrackingWindow)
                {
                    if (peakLevel > configuration.MinPeakThreshold)
                    {
                        targetGain = Mathf.Clamp(configuration.TargetPeakLevel / peakLevel,
                            configuration.MinGain, configuration.MaxGain);
                    }

                    peakLevel = 0f;
                    peakTrackingTime = 0f;
                }
            }

            float gainDiff = targetGain - currentGain;
            float maxGainChange = configuration.GainAdjustSpeed * deltaTime;
            currentGain += Mathf.Clamp(gainDiff, -maxGainChange, maxGainChange);
        }

        public void ProcessAudio(Span<float> audioData, int sampleRate)
        {
            if (audioData.Length == 0) return;

            float deltaTime = (float)audioData.Length / sampleRate;
            bool speechDetected = false;
            float maxInputLevel = 0f;

            // First pass: detect speech and track peak level
            for (int i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];
                float sampleAbs = Mathf.Abs(sample);
                maxInputLevel = Mathf.Max(maxInputLevel, sampleAbs);

                if (sampleAbs > effectiveThreshold)
                {
                    speechDetected = true;
                    peakLevel = Mathf.Max(peakLevel, sampleAbs);
                }
            }

            UpdateAutoGain(deltaTime, speechDetected);

            // Second pass: apply processing and gain
            for (int i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                if (configuration.EnableLowPassFilter)
                {
                    sample = ApplyLowPassFilter2NdOrder(sample, sampleRate);
                }

                if (configuration.EnableNoiseGate)
                {
                    sample = ApplyNoiseGateWithHold(sample, deltaTime, sampleRate);
                }

                sample *= currentGain;
                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }

        private float ApplyLowPassFilter2NdOrder(float input, int sampleRate)
        {
            UpdateLowPassCoefficients(configuration.LowPassCutoffFreq, sampleRate);

            float output = lowPassCoeffs.b0 * input +
                         lowPassCoeffs.b1 * lowPassState.x1 +
                         lowPassCoeffs.b2 * lowPassState.x2 -
                         lowPassCoeffs.a1 * lowPassState.y1 -
                         lowPassCoeffs.a2 * lowPassState.y2;

            // Prevent denormal numbers
            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            // Shift state
            lowPassState.x2 = lowPassState.x1;
            lowPassState.x1 = input;
            lowPassState.y2 = lowPassState.y1;
            lowPassState.y1 = output;

            return output;
        }

        private float ApplyNoiseGateWithHold(float sample, float deltaTime, int sampleRate)
        {
            float sampleAbs = Mathf.Abs(sample);

            fadeInBuffer[fadeInBufferIndex] = sample;
            fadeInBufferIndex = (fadeInBufferIndex + 1) % fadeInBuffer.Length;

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

            if (targetGate > gateSmoothing)
            {
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateAttackTime * 100f, 10f);
            }
            else
            {
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateReleaseTime * 50f, 5f);
            }

            float smoothingFactor = Mathf.Clamp01(gateSpeed);
            gateSmoothing += smoothingFactor * (targetGate - gateSmoothing);
            gateSmoothing = Mathf.Clamp01(gateSmoothing);

            float processedSample = sample;

            if (configuration.EnableGateFadeIn && isGateOpening && gateOpenFadeProgress < 1f)
            {
                fadeInSpeed = 1f / (configuration.NoiseGateAttackTime * sampleRate);
                gateOpenFadeProgress += fadeInSpeed;
                gateOpenFadeProgress = Mathf.Clamp01(gateOpenFadeProgress);

                float fadeCurve = gateOpenFadeProgress * gateOpenFadeProgress * (3f - (2f * gateOpenFadeProgress));

                int lookbackIndex = (fadeInBufferIndex - (int)bufferLookback + fadeInBuffer.Length) % fadeInBuffer.Length;
                float preGateSample = fadeInBuffer[lookbackIndex] * preGateAttenuation;

                processedSample = Mathf.Lerp(preGateSample, sample, fadeCurve);

                if (gateOpenFadeProgress >= 1f)
                {
                    isGateOpening = false;
                }
            }

            if (!gateIsOpen && gateSmoothing < 0.01f)
            {
                ResetFilterStates();
            }

            gateMultiplier = gateSmoothing;
            if (gateSmoothing is > 0.1f and < 0.9f)
            {
                float ratio = (gateSmoothing - 0.1f) * 1.25f;
                gateMultiplier = 0.1f + (0.8f * (ratio * ratio * (3f - (2f * ratio))));
            }

            return processedSample * gateMultiplier;
        }

        private void ResetFilterStates()
        {
            lowPassState = default;
        }

        public void MeasureLowPassFilter(Span<float> audioData, int sampleRate)
        {
            for (int i = 0; i < audioData.Length; i++)
            {
                if (configuration.EnableLowPassFilter)
                {
                    ApplyLowPassFilter2NdOrder(audioData[i], sampleRate);
                }
            }
        }

        public void MeasureNoiseGate(Span<float> audioData, int sampleRate)
        {
            float deltaTime = (float)audioData.Length / sampleRate;
            for (int i = 0; i < audioData.Length; i++)
            {
                if (configuration.EnableNoiseGate)
                {
                    ApplyNoiseGateWithHold(audioData[i], deltaTime, sampleRate);
                }
            }
        }

        public void MeasureGainAdjust(Span<float> audioData)
        {
            for (int i = 0; i < audioData.Length; i++)
            {
                audioData[i] *= currentGain;
            }
        }
    }
}
