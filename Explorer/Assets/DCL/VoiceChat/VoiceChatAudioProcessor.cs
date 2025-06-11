using UnityEngine;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Handles real-time audio processing for voice chat including noise gate
    ///     and low-pass filtering to complement WebRTC's built-in processing.
    /// </summary>
    public class VoiceChatAudioProcessor
    {
        private enum FilterType
        {
            LOW_PASS,
        }

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

            if (fadeInBuffer == null || fadeInBuffer.Length != configuration.FadeInBufferSize) { fadeInBuffer = new float[configuration.FadeInBufferSize]; }

            for (var i = 0; i < fadeInBuffer.Length; i++) { fadeInBuffer[i] = 0f; }

            fadeInBufferIndex = 0;
            isGateOpening = false;
            gateOpenFadeProgress = 0f;
        }

        /// <summary>
        ///     Process audio samples with noise gate and low-pass filtering
        /// </summary>
        public void ProcessAudio(Span<float> audioData, int sampleRate)
        {
            if (audioData.Length == 0) return;

            float deltaTime = (float)audioData.Length / sampleRate;

            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                if (configuration.EnableLowPassFilter) { sample = ApplyLowPassFilter2NdOrder(sample, sampleRate); }

                if (configuration.EnableNoiseGate) { sample = ApplyNoiseGateWithHold(sample, deltaTime, sampleRate); }

                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }

        private float ApplyBiquadFilter(float input, int sampleRate, float cutoffFreq, FilterType filterType,
            float[] prevInputs, float[] prevOutputs)
        {
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

            b0 /= a0;
            b1 /= a0;
            b2 /= a0;
            a1 /= a0;
            a2 /= a0;

            float output = (b0 * input) + (b1 * prevInputs[0]) + (b2 * prevInputs[1])
                           - (a1 * prevOutputs[0]) - (a2 * prevOutputs[1]);

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            prevInputs[1] = prevInputs[0];
            prevInputs[0] = input;
            prevOutputs[1] = prevOutputs[0];
            prevOutputs[0] = output;

            return output;
        }

        private float ApplyLowPassFilter2NdOrder(float input, int sampleRate) =>
            ApplyBiquadFilter(input, sampleRate, configuration.LowPassCutoffFreq, FilterType.LOW_PASS,
                lowPassPrevInputs, lowPassPrevOutputs);

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

        private void ResetFilterStates()
        {
            for (var i = 0; i < 2; i++)
            {
                if (Mathf.Abs(lowPassPrevInputs[i]) < 0.001f && Mathf.Abs(lowPassPrevOutputs[i]) < 0.001f)
                {
                    lowPassPrevInputs[i] = 0f;
                    lowPassPrevOutputs[i] = 0f;
                }
            }
        }
    }
}
