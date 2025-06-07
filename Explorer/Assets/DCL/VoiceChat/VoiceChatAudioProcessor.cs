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
        private int fadeInBufferIndex;
        private bool isGateOpening;
        private float gateOpenFadeProgress;

        private float currentGain;

        private float gateSmoothing;

        public VoiceChatAudioProcessor(VoiceChatConfiguration configuration)
        {
            this.configuration = configuration;
            Reset();
        }

        public void Dispose()
        {
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

            currentGain = 1f;
            peakLevel = 0f;
            gateSmoothing = 0f;
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

            float deltaTime = (float)audioData.Length / sampleRate;

            for (var i = 0; i < audioData.Length; i++)
            {
                float sample = audioData[i];

                if (configuration.EnableBandPassFilter) { sample = ApplyBandPassFilter(sample, sampleRate); }
                if (configuration.EnableNoiseGate) { sample = ApplyNoiseGateWithHold(sample, deltaTime); }
                if (configuration.EnableAutoGainControl) { sample = ApplyAGC(sample); }

                audioData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
        }

        private float ApplyBandPassFilter(float input, int sampleRate)
        {
            float sample = ApplyDcBlockingFilter(input, sampleRate);
            sample = ApplyHighPassFilter2NdOrder(sample, sampleRate);
            sample = ApplyLowPassFilter2NdOrder(sample, sampleRate);

            return sample;
        }

        private float ApplyDcBlockingFilter(float input, int sampleRate)
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

        private float ApplyHighPassFilter2NdOrder(float input, int sampleRate)
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

            float output = (b0 * input) + (b1 * highPassPrevInputs[0]) + (b2 * highPassPrevInputs[1])
                         - (a1 * highPassPrevOutputs[0]) - (a2 * highPassPrevOutputs[1]);

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            highPassPrevInputs[1] = highPassPrevInputs[0];
            highPassPrevInputs[0] = input;
            highPassPrevOutputs[1] = highPassPrevOutputs[0];
            highPassPrevOutputs[0] = output;

            return output;
        }

        private float ApplyLowPassFilter2NdOrder(float input, int sampleRate)
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

            float output = (b0 * input) + (b1 * lowPassPrevInputs[0]) + (b2 * lowPassPrevInputs[1])
                         - (a1 * lowPassPrevOutputs[0]) - (a2 * lowPassPrevOutputs[1]);

            if (Mathf.Abs(output) < 1e-10f) output = 0f;

            lowPassPrevInputs[1] = lowPassPrevInputs[0];
            lowPassPrevInputs[0] = input;
            lowPassPrevOutputs[1] = lowPassPrevOutputs[0];
            lowPassPrevOutputs[0] = output;

            return output;
        }

        private float ApplyNoiseGateWithHold(float sample, float deltaTime)
        {
            float sampleAbs = Mathf.Abs(sample);

            fadeInBuffer[fadeInBufferIndex] = sample;
            fadeInBufferIndex = (fadeInBufferIndex + 1) % fadeInBuffer.Length;

            float effectiveThreshold = configuration.NoiseGateThreshold;

            bool speechDetected = sampleAbs > effectiveThreshold;
            bool wasGateOpen = gateIsOpen;

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

            if (targetGate > gateSmoothing)
            {
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateAttackTime * 100f, 10f);
            }
            else
            {
                gateSpeed = 1f / Mathf.Max(configuration.NoiseGateReleaseTime * 50f, 5f);
            }

            float smoothingFactor = Mathf.Clamp01(gateSpeed);
            gateSmoothing = gateSmoothing + (targetGate - gateSmoothing) * smoothingFactor;
            gateSmoothing = Mathf.Clamp01(gateSmoothing);

            float processedSample = sample;

            if (configuration.EnableGateFadeIn && isGateOpening && gateOpenFadeProgress < 1f)
            {
                float fadeInSpeed = 1f / (configuration.NoiseGateAttackTime * 48000f);
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

            if (!gateIsOpen && gateSmoothing < 0.01f)
            {
                ResetFilterStates();
            }

            float gateMultiplier = gateSmoothing;
            if (gateSmoothing > 0.1f && gateSmoothing < 0.9f)
            {
                float ratio = (gateSmoothing - 0.1f) / 0.8f;
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

        private float ApplyAGC(float sample)
        {
            float sampleAbs = Mathf.Abs(sample);

            var peakDecay = 0.9995f;
            var peakAttack = 0.1f;

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
                targetGain = Mathf.Clamp(targetGain, 0.2f, 3f);

                float gainDifference = Mathf.Abs(targetGain - currentGain);
                float baseSpeed = configuration.AGCResponseSpeed * 0.002f;

                float adaptiveSpeed = gainDifference > 0.5f ? baseSpeed * 0.3f : baseSpeed;

                float smoothingWindow = 0.95f;
                currentGain = currentGain * smoothingWindow + targetGain * (1f - smoothingWindow) * adaptiveSpeed;
                currentGain = Mathf.Clamp(currentGain, 0.2f, 3f);
            }

            float processedSample = sample * currentGain;

            if (Mathf.Abs(processedSample) > 0.95f)
            {
                float sign = Mathf.Sign(processedSample);
                float magnitude = Mathf.Abs(processedSample);

                float compressedMagnitude = 0.95f + (magnitude - 0.95f) * 0.1f;
                processedSample = sign * Mathf.Min(compressedMagnitude, 0.99f);
            }

            return processedSample;
        }
    }
}
