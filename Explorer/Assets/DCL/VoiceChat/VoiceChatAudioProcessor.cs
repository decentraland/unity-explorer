using UnityEngine;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Performs real-time audio preprocessing for voice chat, including noise gating, low-pass filtering, and auto-gain control.
    ///     Designed to improve audio quality before transmission or playback.
    /// </summary>
    public class VoiceChatAudioProcessor
    {
        private readonly VoiceChatConfiguration configuration;

        private readonly float[] lowPassPrevInputs = new float[2];
        private readonly float[] lowPassPrevOutputs = new float[2];

        private float peakLevel;
        private bool gateIsOpen;
        private float lastSpeechTime;
        private float[] fadeInBuffer;
        private int fadeInBufferIndex;
        private bool isGateOpening;
        private float gateOpenFadeProgress;
        private float gateSmoothing;
        private float targetGain = 1f;
        private float currentGain = 1f;
        private float peakTrackingTime;

        private BiquadState lowPassState;
        private BiquadCoefficients lowPassCoeffs;
        private float lastLowPassCutoff = -1f;
        private int lastLowPassSampleRate = -1;

        public VoiceChatAudioProcessor(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;
            Reset();
        }

        public void Dispose() { }

        public void Reset()
        {
            for (var i = 0; i < 2; i++)
            {
                lowPassPrevInputs[i] = 0f;
                lowPassPrevOutputs[i] = 0f;
            }

            peakLevel = 0f;
            gateSmoothing = 0f;
            gateIsOpen = false;
            lastSpeechTime = 0f;
            currentGain = 1f;
            targetGain = 1f;
            peakTrackingTime = 0f;

            if (fadeInBuffer == null || fadeInBuffer.Length != configuration.FadeInBufferSize) { fadeInBuffer = new float[configuration.FadeInBufferSize]; }

            for (var i = 0; i < fadeInBuffer.Length; i++) { fadeInBuffer[i] = 0f; }

            fadeInBufferIndex = 0;
            isGateOpening = false;
            gateOpenFadeProgress = 0f;

            lowPassState = default(BiquadState);
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
            float alpha = sinw / (2f * 0.7071f);

            float b0 = (1f - cosw) / 2f;
            float b1 = 1f - cosw;
            float b2 = (1f - cosw) / 2f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosw;
            float a2 = 1f - alpha;

            lowPassCoeffs.b0 = b0 / a0;
            lowPassCoeffs.b1 = b1 / a0;
            lowPassCoeffs.b2 = b2 / a0;
            lowPassCoeffs.a1 = a1 / a0;
            lowPassCoeffs.a2 = a2 / a0;

            lastLowPassCutoff = cutoffFreq;
            lastLowPassSampleRate = sampleRate;
        }

        /// <summary>
        ///     Adjusts gain to maintain a target peak level, only when speech is detected.
        ///     Smoothly transitions gain to avoid abrupt changes.
        /// </summary>
        private void UpdateAutoGain(float deltaTime, bool speechDetected)
        {
            if (speechDetected)
            {
                peakTrackingTime += deltaTime;

                if (peakTrackingTime >= configuration.PeakTrackingWindow)
                {
                    if (peakLevel > configuration.MinPeakThreshold) { targetGain = Mathf.Clamp(configuration.TargetPeakLevel / peakLevel, configuration.MinGain, configuration.MaxGain); }

                    peakLevel = 0f;
                    peakTrackingTime = 0f;
                }
            }

            float gainDiff = targetGain - currentGain;
            float maxGainChange = configuration.GainAdjustSpeed * deltaTime;
            currentGain += Mathf.Clamp(gainDiff, -maxGainChange, maxGainChange);
        }

        /// <summary>
        ///     Processes a buffer of audio samples in-place, applying noise gate, low-pass filtering, and auto-gain control.
        ///     Intended for real-time voice chat audio preprocessing before transmission or playback.
        /// </summary>
        public void ProcessAudio(Span<float> audioData, int sampleRate)
        {
            if (audioData.Length == 0) return;

            float deltaTime = (float)audioData.Length / sampleRate;
            var speechDetected = false;
            var maxInputLevel = 0f;
            var maxOutputLevel = 0f;

            // First pass: detect speech and track peak level
            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];
                float sampleAbs = Mathf.Abs(sample);
                maxInputLevel = Mathf.Max(maxInputLevel, sampleAbs);

                if (sampleAbs > configuration.NoiseGateThreshold)
                {
                    speechDetected = true;
                    peakLevel = Mathf.Max(peakLevel, sampleAbs);
                }
            }

            UpdateAutoGain(deltaTime, speechDetected);

            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                if (configuration.EnableLowPassFilter) { sample = ApplyLowPassFilter2NdOrder(sample, sampleRate); }

                if (configuration.EnableNoiseGate) { sample = ApplyNoiseGateWithHold(sample, deltaTime, sampleRate); }

                sample *= currentGain;
                maxOutputLevel = Mathf.Max(maxOutputLevel, Mathf.Abs(sample));

                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }

        /// <summary>
        ///     Applies a second-order low-pass filter to the input sample using the configured cutoff frequency.
        ///     See: https://webaudio.github.io/Audio-EQ-Cookbook/audio-eq-cookbook.html
        /// </summary>
        private float ApplyLowPassFilter2NdOrder(float input, int sampleRate)
        {
            UpdateLowPassCoefficients(configuration.LowPassCutoffFreq, sampleRate);

            float output = (lowPassCoeffs.b0 * input)
                           + (lowPassCoeffs.b1 * lowPassState.x1)
                           + (lowPassCoeffs.b2 * lowPassState.x2)
                           - (lowPassCoeffs.a1 * lowPassState.y1)
                           - (lowPassCoeffs.a2 * lowPassState.y2);

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            // Shift state
            lowPassState.x2 = lowPassState.x1;
            lowPassState.x1 = input;
            lowPassState.y2 = lowPassState.y1;
            lowPassState.y1 = output;

            return output;
        }

        /// <summary>
        ///     Applies a noise gate with hold and fade-in logic to the sample,
        ///     muting low-level noise and smoothing transitions.
        /// </summary>
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

            if (targetGate > gateSmoothing) { gateSpeed = 1f / Mathf.Max(configuration.NoiseGateAttackTime * 100f, 10f); }
            else { gateSpeed = 1f / Mathf.Max(configuration.NoiseGateReleaseTime * 50f, 5f); }

            float smoothingFactor = Mathf.Clamp01(gateSpeed);
            gateSmoothing += smoothingFactor * (targetGate - gateSmoothing);
            gateSmoothing = Mathf.Clamp01(gateSmoothing);

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

            if (!gateIsOpen && gateSmoothing < 0.01f) { ResetFilterStates(); }

            float gateMultiplier = gateSmoothing;

            if (gateSmoothing is > 0.1f and < 0.9f)
            {
                float ratio = (gateSmoothing - 0.1f) / 0.8f;
                gateMultiplier = 0.1f + (0.8f * (ratio * ratio * (3f - (2f * ratio))));
            }

            return processedSample * gateMultiplier;
        }

        /// <summary>
        ///     Resets the filter state buffers to prevent artifacts when the gate closes.
        /// </summary>
        private void ResetFilterStates()
        {
            lowPassState = default(BiquadState);
        }

        private struct BiquadState
        {
            public float x1, x2, y1, y2;
        }

        private struct BiquadCoefficients
        {
            public float b0, b1, b2, a1, a2;
        }
    }
}
