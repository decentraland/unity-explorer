using System;
using UnityEngine;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    public class VoiceChatAudioFeedbackSuppressor
    {
        private const int FEEDBACK_DETECTION_BUFFER_SIZE = 4096; // ~85ms at 48kHz
        private const float DEFAULT_CORRELATION_THRESHOLD = 0.7f;
        private const float DEFAULT_SUPPRESSION_STRENGTH = 0.3f;
        private const float DEFAULT_ATTACK_RATE = 0.1f;
        private const float DEFAULT_RELEASE_RATE = 0.05f;

        private readonly float[] speakerBuffer = new float[FEEDBACK_DETECTION_BUFFER_SIZE];
        private readonly float[] microphoneBuffer = new float[FEEDBACK_DETECTION_BUFFER_SIZE];
        private int bufferIndex = 0;
        private bool feedbackDetected = false;
        private float feedbackSuppressionLevel = 0f;
        private VoiceChatConfiguration configuration;

        public bool IsEnabled => configuration?.EnableFeedbackSuppression == true;
        public bool IsFeedbackDetected => feedbackDetected;
        public float CurrentSuppressionLevel => feedbackSuppressionLevel;

        public void Initialize(VoiceChatConfiguration config)
        {
            configuration = config;
            Reset();
        }

        public void Reset()
        {
            feedbackDetected = false;
            feedbackSuppressionLevel = 0f;
            bufferIndex = 0;
            
            Array.Clear(speakerBuffer, 0, speakerBuffer.Length);
            Array.Clear(microphoneBuffer, 0, microphoneBuffer.Length);
        }

        public bool ProcessAudio(float[] microphoneData, int channels, int samplesPerChannel, 
                               float[] speakerData, int speakerSamples)
        {
            if (!IsEnabled || microphoneData == null || speakerData == null)
                return false;

            Span<float> monoSpan = microphoneBuffer.AsSpan(0, samplesPerChannel);
            
            if (channels > 1)
            {
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < channels; ch++)
                        sum += microphoneData[i * channels + ch];
                    monoSpan[i] = sum / channels;
                }
            }
            else
            {
                microphoneData.AsSpan(0, samplesPerChannel).CopyTo(monoSpan);
            }

            UpdateBuffers(monoSpan, speakerData, speakerSamples);

            float correlation = CalculateCrossCorrelation();
            
            bool wasFeedbackDetected = feedbackDetected;
            float threshold = configuration?.FeedbackCorrelationThreshold ?? DEFAULT_CORRELATION_THRESHOLD;
            feedbackDetected = correlation > threshold;
            
            if (feedbackDetected)
            {
                float attackRate = configuration?.FeedbackSuppressionAttackRate ?? DEFAULT_ATTACK_RATE;
                float maxStrength = configuration?.FeedbackSuppressionStrength ?? DEFAULT_SUPPRESSION_STRENGTH;
                feedbackSuppressionLevel = Mathf.Min(feedbackSuppressionLevel + attackRate, maxStrength);
                
                if (!wasFeedbackDetected)
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Feedback detected! Correlation: {correlation:F3}, Suppression: {feedbackSuppressionLevel:F3}");
            }
            else
            {
                float releaseRate = configuration?.FeedbackSuppressionReleaseRate ?? DEFAULT_RELEASE_RATE;
                feedbackSuppressionLevel = Mathf.Max(feedbackSuppressionLevel - releaseRate, 0f);
            }

            return feedbackSuppressionLevel > 0.01f;
        }

        public void ApplySuppression(float[] data, int channels, int samplesPerChannel)
        {
            if (feedbackSuppressionLevel <= 0.01f)
                return;

            float suppression = 1f - feedbackSuppressionLevel;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i * channels + ch;
                    data[index] *= suppression;
                }
            }
        }

        private void UpdateBuffers(Span<float> microphoneData, float[] speakerData, int speakerSamples)
        {
            for (int i = 0; i < microphoneData.Length; i++)
            {
                microphoneBuffer[bufferIndex] = microphoneData[i];
                bufferIndex = (bufferIndex + 1) % FEEDBACK_DETECTION_BUFFER_SIZE;
            }

            int speakerBufferIndex = bufferIndex;
            for (int i = 0; i < Mathf.Min(speakerSamples, FEEDBACK_DETECTION_BUFFER_SIZE); i++)
            {
                speakerBuffer[speakerBufferIndex] = speakerData[i];
                speakerBufferIndex = (speakerBufferIndex + 1) % FEEDBACK_DETECTION_BUFFER_SIZE;
            }
        }

        private float CalculateCrossCorrelation()
        {
            float correlation = 0f;
            float micEnergy = 0f;
            float speakerEnergy = 0f;
            float crossEnergy = 0f;

            for (int i = 0; i < FEEDBACK_DETECTION_BUFFER_SIZE; i++)
            {
                float mic = microphoneBuffer[i];
                float spk = speakerBuffer[i];
                
                micEnergy += mic * mic;
                speakerEnergy += spk * spk;
                crossEnergy += mic * spk;
            }

            if (micEnergy > 0.001f && speakerEnergy > 0.001f)
            {
                correlation = crossEnergy / Mathf.Sqrt(micEnergy * speakerEnergy);
            }

            return Mathf.Abs(correlation);
        }

        public VoiceChatConfiguration GetConfiguration() => configuration;

        public void UpdateConfiguration(VoiceChatConfiguration newConfig)
        {
            configuration = newConfig;
            Reset();
        }
    }
} 