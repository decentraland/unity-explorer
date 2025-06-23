using System;
using UnityEngine;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    public static class VoiceChatAudioEchoCancellation
    {
        // Increased buffer size for better detection (WebRTC uses much larger buffers)
        private const int FEEDBACK_DETECTION_BUFFER_SIZE = 8192; // ~170ms at 48kHz
        private const int DELAY_ESTIMATION_BUFFER_SIZE = 16384; // ~340ms for delay estimation
        
        // More sensitive thresholds based on WebRTC patterns
        private const float DEFAULT_CORRELATION_THRESHOLD = 0.25f; // Lowered from 0.4f for more sensitivity
        private const float DEFAULT_SUPPRESSION_STRENGTH = 0.7f; // Increased from 0.5f for stronger suppression
        private const float DEFAULT_ATTACK_RATE = 0.2f; // Increased from 0.15f for faster response
        private const float DEFAULT_RELEASE_RATE = 0.02f; // Decreased from 0.03f for slower release
        
        // Delay estimation constants
        private const int MIN_DELAY_SAMPLES = 512; // ~10ms at 48kHz
        private const int MAX_DELAY_SAMPLES = 8192; // ~170ms at 48kHz
        private const float DELAY_ESTIMATION_ALPHA = 0.95f; // Smoothing factor

        private static readonly float[] speakerBuffer = new float[FEEDBACK_DETECTION_BUFFER_SIZE];
        private static readonly float[] microphoneBuffer = new float[FEEDBACK_DETECTION_BUFFER_SIZE];
        private static readonly float[] delayEstimationBuffer = new float[DELAY_ESTIMATION_BUFFER_SIZE];
        
        private static int bufferIndex = 0;
        private static int delayBufferIndex = 0;
        private static bool feedbackDetected = false;
        private static float feedbackSuppressionLevel = 0f;
        private static VoiceChatConfiguration configuration;
        
        // Delay estimation state
        private static int estimatedDelay = 0;
        private static bool delayEstimationInitialized = false;
        private static float lastCorrelation = 0f;
        private static int consecutiveDetections = 0;
        private static int consecutiveNonDetections = 0;
        
        // Logging state
        private static int logCounter = 0;
        private const int LOG_INTERVAL = 100; // Log every 100 frames

        public static bool IsEnabled => configuration?.EnableFeedbackSuppression == true;

        public static void Initialize(VoiceChatConfiguration config)
        {
            configuration = config;
            Debug.LogWarning("SUPRESSOR: Initializing audio feedback suppressor with improved detection");
            Debug.LogWarning($"SUPRESSOR: Buffer sizes - Detection: {FEEDBACK_DETECTION_BUFFER_SIZE}, Delay: {DELAY_ESTIMATION_BUFFER_SIZE}");
            Debug.LogWarning($"SUPRESSOR: Default threshold: {DEFAULT_CORRELATION_THRESHOLD}, Strength: {DEFAULT_SUPPRESSION_STRENGTH}");
            Reset();
        }

        public static void Reset()
        {
            feedbackDetected = false;
            feedbackSuppressionLevel = 0f;
            bufferIndex = 0;
            delayBufferIndex = 0;
            estimatedDelay = 0;
            delayEstimationInitialized = false;
            lastCorrelation = 0f;
            consecutiveDetections = 0;
            consecutiveNonDetections = 0;
            logCounter = 0;

            Array.Clear(speakerBuffer, 0, speakerBuffer.Length);
            Array.Clear(microphoneBuffer, 0, microphoneBuffer.Length);
            Array.Clear(delayEstimationBuffer, 0, delayEstimationBuffer.Length);
            Debug.LogWarning("SUPRESSOR: Reset feedback detection buffers and state");
        }

        public static bool ProcessAudio(float[] microphoneData, int channels, int samplesPerChannel,
                               float[] speakerData, int speakerSamples)
        {
            if (!IsEnabled || microphoneData == null || speakerData == null)
                return false;

            logCounter++;
            bool shouldLog = logCounter % LOG_INTERVAL == 0;

            if (shouldLog)
            {
                Debug.LogWarning($"SUPRESSOR: Processing audio - Mic: {samplesPerChannel} samples, Speaker: {speakerSamples} samples, Channels: {channels}");
            }

            // Convert microphone to mono
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

            // Update buffers with proper delay compensation
            UpdateBuffersWithDelay(monoSpan, speakerData, speakerSamples);

            // Calculate correlation with delay compensation
            float correlation = CalculateCrossCorrelationWithDelay();

            // Update delay estimation
            UpdateDelayEstimation(correlation);

            bool wasFeedbackDetected = feedbackDetected;
            float threshold = configuration?.FeedbackCorrelationThreshold ?? DEFAULT_CORRELATION_THRESHOLD;
            
            if (shouldLog)
            {
                Debug.LogWarning($"SUPRESSOR: Correlation: {correlation:F3}, Threshold: {threshold:F3}, Delay: {estimatedDelay} samples, Initialized: {delayEstimationInitialized}");
            }
            
            // Use hysteresis to prevent rapid switching
            if (correlation > threshold)
            {
                consecutiveDetections++;
                consecutiveNonDetections = 0;
                
                if (shouldLog)
                {
                    Debug.LogWarning($"SUPRESSOR: Above threshold - Consecutive detections: {consecutiveDetections}");
                }
                
                // Require fewer consecutive detections to trigger (more sensitive)
                if (consecutiveDetections >= 2)
                {
                    feedbackDetected = true;
                }
            }
            else
            {
                consecutiveNonDetections++;
                consecutiveDetections = 0;
                
                if (shouldLog)
                {
                    Debug.LogWarning($"SUPRESSOR: Below threshold - Consecutive non-detections: {consecutiveNonDetections}");
                }
                
                // Require more consecutive non-detections to clear (more stable)
                if (consecutiveNonDetections >= 8)
                {
                    feedbackDetected = false;
                }
            }

            if (feedbackDetected)
            {
                if (!wasFeedbackDetected)
                {
                    Debug.LogWarning($"SUPRESSOR: Feedback detected! Correlation: {correlation:F3}, Threshold: {threshold:F3}, Delay: {estimatedDelay} samples");
                }

                float attackRate = configuration?.FeedbackSuppressionAttackRate ?? DEFAULT_ATTACK_RATE;
                float maxStrength = configuration?.FeedbackSuppressionStrength ?? DEFAULT_SUPPRESSION_STRENGTH;
                feedbackSuppressionLevel = Mathf.Min(feedbackSuppressionLevel + attackRate, maxStrength);

                if (shouldLog)
                {
                    Debug.LogWarning($"SUPRESSOR: Applying suppression. Level: {feedbackSuppressionLevel:F3}, Max: {maxStrength:F3}, Attack rate: {attackRate:F3}");
                }
            }
            else
            {
                if (wasFeedbackDetected)
                {
                    Debug.LogWarning($"SUPRESSOR: Feedback cleared. Correlation: {correlation:F3}, Threshold: {threshold:F3}");
                }

                float releaseRate = configuration?.FeedbackSuppressionReleaseRate ?? DEFAULT_RELEASE_RATE;
                feedbackSuppressionLevel = Mathf.Max(feedbackSuppressionLevel - releaseRate, 0f);
                
                if (shouldLog && feedbackSuppressionLevel > 0.01f)
                {
                    Debug.LogWarning($"SUPRESSOR: Releasing suppression. Level: {feedbackSuppressionLevel:F3}, Release rate: {releaseRate:F3}");
                }
            }

            return feedbackSuppressionLevel > 0.01f;
        }

        public static void ApplySuppression(float[] data, int channels, int samplesPerChannel)
        {
            if (feedbackSuppressionLevel <= 0.01f)
                return;

            float suppression = 1f - feedbackSuppressionLevel;
            
            if (logCounter % LOG_INTERVAL == 0)
            {
                Debug.LogWarning($"SUPRESSOR: Applying audio suppression. Level: {feedbackSuppressionLevel:F3}, Gain: {suppression:F3}, Samples: {samplesPerChannel}");
            }

            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i * channels + ch;
                    data[index] *= suppression;
                }
            }
        }

        private static void UpdateBuffersWithDelay(Span<float> microphoneData, float[] speakerData, int speakerSamples)
        {
            // Update microphone buffer
            for (int i = 0; i < microphoneData.Length; i++)
            {
                microphoneBuffer[bufferIndex] = microphoneData[i];
                bufferIndex = (bufferIndex + 1) % FEEDBACK_DETECTION_BUFFER_SIZE;
            }

            // Update delay estimation buffer (larger buffer for delay estimation)
            for (int i = 0; i < Mathf.Min(speakerSamples, DELAY_ESTIMATION_BUFFER_SIZE); i++)
            {
                delayEstimationBuffer[delayBufferIndex] = speakerData[i];
                delayBufferIndex = (delayBufferIndex + 1) % DELAY_ESTIMATION_BUFFER_SIZE;
            }

            // Update speaker buffer with delay compensation
            int speakerBufferIndex = bufferIndex;
            for (int i = 0; i < Mathf.Min(speakerSamples, FEEDBACK_DETECTION_BUFFER_SIZE); i++)
            {
                speakerBuffer[speakerBufferIndex] = speakerData[i];
                speakerBufferIndex = (speakerBufferIndex + 1) % FEEDBACK_DETECTION_BUFFER_SIZE;
            }
        }

        private static float CalculateCrossCorrelationWithDelay()
        {
            if (!delayEstimationInitialized || estimatedDelay <= 0)
            {
                float simpleCorrelation = CalculateCrossCorrelation();
                if (logCounter % LOG_INTERVAL == 0)
                {
                    Debug.LogWarning($"SUPRESSOR: Using simple correlation: {simpleCorrelation:F3} (delay not estimated yet)");
                }
                return simpleCorrelation;
            }

            // Calculate correlation with delay compensation
            float correlation = 0f;
            float micEnergy = 0f;
            float speakerEnergy = 0f;
            float crossEnergy = 0f;

            for (int i = 0; i < FEEDBACK_DETECTION_BUFFER_SIZE - estimatedDelay; i++)
            {
                float mic = microphoneBuffer[i];
                float spk = speakerBuffer[(i + estimatedDelay) % FEEDBACK_DETECTION_BUFFER_SIZE];

                micEnergy += mic * mic;
                speakerEnergy += spk * spk;
                crossEnergy += mic * spk;
            }

            if (micEnergy > 0.001f && speakerEnergy > 0.001f)
            {
                correlation = crossEnergy / Mathf.Sqrt(micEnergy * speakerEnergy);
            }

            if (logCounter % LOG_INTERVAL == 0)
            {
                Debug.LogWarning($"SUPRESSOR: Delay-compensated correlation: {correlation:F3}, Delay: {estimatedDelay} samples, Mic energy: {micEnergy:F6}, Spk energy: {speakerEnergy:F6}");
            }

            return Mathf.Abs(correlation);
        }

        private static float CalculateCrossCorrelation()
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

        private static void UpdateDelayEstimation(float correlation)
        {
            // Only update delay estimation when we have significant correlation (lowered threshold)
            if (correlation > 0.2f)
            {
                int bestDelay = FindBestDelay();
                
                if (bestDelay >= MIN_DELAY_SAMPLES && bestDelay <= MAX_DELAY_SAMPLES)
                {
                    if (!delayEstimationInitialized)
                    {
                        estimatedDelay = bestDelay;
                        delayEstimationInitialized = true;
                        Debug.LogWarning($"SUPRESSOR: Delay estimation initialized! Best delay: {bestDelay} samples ({bestDelay / 48f:F1}ms)");
                    }
                    else
                    {
                        int oldDelay = estimatedDelay;
                        // Smooth delay updates
                        estimatedDelay = (int)(DELAY_ESTIMATION_ALPHA * estimatedDelay + (1f - DELAY_ESTIMATION_ALPHA) * bestDelay);
                        
                        if (logCounter % LOG_INTERVAL == 0 && Mathf.Abs(oldDelay - estimatedDelay) > 10)
                        {
                            Debug.LogWarning($"SUPRESSOR: Delay updated: {oldDelay} -> {estimatedDelay} samples (best: {bestDelay})");
                        }
                    }
                }
            }
        }

        private static int FindBestDelay()
        {
            int bestDelay = 0;
            float bestCorrelation = 0f;

            // Search for the delay that gives the highest correlation
            for (int delay = MIN_DELAY_SAMPLES; delay <= MAX_DELAY_SAMPLES; delay += 64) // Step by 64 samples for performance
            {
                float delayCorrelation = 0f;
                float micEnergy = 0f;
                float spkEnergy = 0f;
                float crossEnergy = 0f;

                for (int i = 0; i < FEEDBACK_DETECTION_BUFFER_SIZE - delay; i++)
                {
                    float mic = microphoneBuffer[i];
                    float spk = delayEstimationBuffer[(delayBufferIndex - delay - i + DELAY_ESTIMATION_BUFFER_SIZE) % DELAY_ESTIMATION_BUFFER_SIZE];

                    micEnergy += mic * mic;
                    spkEnergy += spk * spk;
                    crossEnergy += mic * spk;
                }

                if (micEnergy > 0.001f && spkEnergy > 0.001f)
                {
                    delayCorrelation = crossEnergy / Mathf.Sqrt(micEnergy * spkEnergy);
                    
                    if (delayCorrelation > bestCorrelation)
                    {
                        bestCorrelation = delayCorrelation;
                        bestDelay = delay;
                    }
                }
            }

            if (logCounter % LOG_INTERVAL == 0 && bestCorrelation > 0.2f)
            {
                Debug.LogWarning($"SUPRESSOR: Delay search found best delay: {bestDelay} samples with correlation: {bestCorrelation:F3}");
            }

            return bestDelay;
        }
    }
}
