using System;
using UnityEngine;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    public static class VoiceChatAudioEchoCancellation
    {
        private const int FFT_SIZE = 512;
        private const int FRAME_SIZE = FFT_SIZE / 2;
        private const int NUM_PARTITIONS = 12;
        private const int BUFFER_SIZE = FRAME_SIZE * NUM_PARTITIONS;

        private const float DEFAULT_CORRELATION_THRESHOLD = 0.15f;
        private const float DEFAULT_SUPPRESSION_STRENGTH = 0.8f;
        private const float DEFAULT_ATTACK_RATE = 0.25f;
        private const float DEFAULT_RELEASE_RATE = 0.01f;

        private const float ADAPTIVE_FILTER_LEARNING_RATE = 0.1f;
        private const float ECHO_PATH_LEARNING_RATE = 0.05f;
        private const float MIN_ECHO_PATH_GAIN = 0.01f;
        private const float MAX_ECHO_PATH_GAIN = 2.0f;
        private const int LOG_INTERVAL = 100;

        private static readonly float[] speakerBuffer = new float[BUFFER_SIZE];
        private static readonly float[] microphoneBuffer = new float[BUFFER_SIZE];
        private static readonly float[] echoPathBuffer = new float[BUFFER_SIZE];
        private static readonly float[] adaptiveFilter = new float[BUFFER_SIZE];
        private static readonly float[] fftBuffer = new float[FFT_SIZE * 2];
        private static readonly float[] fftOutput = new float[FFT_SIZE * 2];

        private static int bufferIndex;
        private static bool echoDetected;
        private static float echoCancellationLevel;
        private static VoiceChatConfiguration configuration;

        private static float echoPathGain = 1.0f;
        private static float adaptiveFilterGain = 1.0f;
        private static int consecutiveDetections;
        private static int consecutiveNonDetections;
        private static float lastEchoLevel;

        private static int logCounter;

        public static bool IsEnabled => configuration?.EnableAudioEchoCancellation == true;

        public static void Initialize(VoiceChatConfiguration config)
        {
            configuration = config;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Initializing advanced AEC with frequency-domain processing");
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"FFT size: {FFT_SIZE}, Frame size: {FRAME_SIZE}, Partitions: {NUM_PARTITIONS}");
            Reset();
        }

        public static void Reset()
        {
            echoDetected = false;
            echoCancellationLevel = 0f;
            bufferIndex = 0;
            echoPathGain = 1.0f;
            adaptiveFilterGain = 1.0f;
            consecutiveDetections = 0;
            consecutiveNonDetections = 0;
            lastEchoLevel = 0f;
            logCounter = 0;

            Array.Clear(speakerBuffer, 0, speakerBuffer.Length);
            Array.Clear(microphoneBuffer, 0, microphoneBuffer.Length);
            Array.Clear(echoPathBuffer, 0, echoPathBuffer.Length);
            Array.Clear(adaptiveFilter, 0, adaptiveFilter.Length);
            Array.Clear(fftBuffer, 0, fftBuffer.Length);
            Array.Clear(fftOutput, 0, fftOutput.Length);

            ReportHub.Log(ReportCategory.VOICE_CHAT, "Reset all AEC buffers and state");
        }

        public static void ForceResetEchoPath()
        {
            echoPathGain = 1.0f;
            adaptiveFilterGain = 1.0f;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Force reset echo path model");
        }

        public static bool ProcessAudio(float[] microphoneData, int channels, int samplesPerChannel,
            float[] speakerData, int speakerSamples)
        {
            if (!IsEnabled || microphoneData == null || speakerData == null)
                return false;

            logCounter++;
            bool shouldLog = logCounter % LOG_INTERVAL == 0;

            if (shouldLog) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Processing audio - Mic: {samplesPerChannel} samples, Speaker: {speakerSamples} samples"); }

            Span<float> monoSpan = microphoneBuffer.AsSpan(0, samplesPerChannel);

            if (channels > 1)
            {
                for (var i = 0; i < samplesPerChannel; i++)
                {
                    var sum = 0f;

                    for (var ch = 0; ch < channels; ch++)
                        sum += microphoneData[(i * channels) + ch];

                    monoSpan[i] = sum / channels;
                }
            }
            else { microphoneData.AsSpan(0, samplesPerChannel).CopyTo(monoSpan); }

            UpdateBuffers(monoSpan, speakerData, speakerSamples);

            float echoLevel = CalculateEchoLevel();
            float correlation = CalculateFrequencyDomainCorrelation();

            UpdateEchoPathModel(echoLevel, correlation);

            bool wasEchoDetected = echoDetected;
            float threshold = configuration?.EchoCorrelationThreshold ?? DEFAULT_CORRELATION_THRESHOLD;

            if (shouldLog)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Echo level: {echoLevel:F3}, Correlation: {correlation:F3}, Threshold: {threshold:F3}");
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Echo path gain: {echoPathGain:F3}, Adaptive gain: {adaptiveFilterGain:F3}");
            }

            if (echoLevel > threshold && correlation > threshold * 0.5f)
            {
                consecutiveDetections++;
                consecutiveNonDetections = 0;

                if (shouldLog) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Above threshold - Consecutive detections: {consecutiveDetections}"); }

                if (consecutiveDetections >= 2) { echoDetected = true; }
            }
            else
            {
                consecutiveNonDetections++;
                consecutiveDetections = 0;

                if (shouldLog) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Below threshold - Consecutive non-detections: {consecutiveNonDetections}"); }

                if (consecutiveNonDetections >= 10) { echoDetected = false; }
            }

            if (echoDetected)
            {
                if (!wasEchoDetected) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Feedback detected! Echo level: {echoLevel:F3}, Correlation: {correlation:F3}"); }

                float attackRate = configuration?.EchoCancellationAttackRate ?? DEFAULT_ATTACK_RATE;
                float maxStrength = configuration?.EchoCancellationStrength ?? DEFAULT_SUPPRESSION_STRENGTH;
                echoCancellationLevel = Mathf.Min(echoCancellationLevel + attackRate, maxStrength);

                if (shouldLog) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Applying suppression. Level: {echoCancellationLevel:F3}, Max: {maxStrength:F3}"); }
            }
            else
            {
                if (wasEchoDetected) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Feedback cleared. Echo level: {echoLevel:F3}, Correlation: {correlation:F3}"); }

                float releaseRate = configuration?.EchoCancellationReleaseRate ?? DEFAULT_RELEASE_RATE;
                echoCancellationLevel = Mathf.Max(echoCancellationLevel - releaseRate, 0f);

                if (shouldLog && echoCancellationLevel > 0.01f) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Releasing suppression. Level: {echoCancellationLevel:F3}"); }
            }

            lastEchoLevel = echoLevel;
            return echoCancellationLevel > 0.01f;
        }

        public static void ApplySuppression(float[] data, int channels, int samplesPerChannel)
        {
            if (echoCancellationLevel <= 0.01f)
                return;

            float suppression = 1f - echoCancellationLevel;

            if (logCounter % LOG_INTERVAL == 0) { ReportHub.Log(ReportCategory.VOICE_CHAT, $"Applying audio suppression. Level: {echoCancellationLevel:F3}, Gain: {suppression:F3}"); }

            for (var i = 0; i < samplesPerChannel; i++)
            {
                for (var ch = 0; ch < channels; ch++)
                {
                    int index = (i * channels) + ch;
                    data[index] *= suppression;
                }
            }
        }

        private static void UpdateBuffers(Span<float> microphoneData, float[] speakerData, int speakerSamples)
        {
            for (var i = 0; i < microphoneData.Length; i++)
            {
                microphoneBuffer[bufferIndex] = microphoneData[i];
                bufferIndex = (bufferIndex + 1) % BUFFER_SIZE;
            }

            int speakerBufferIndex = bufferIndex;

            for (var i = 0; i < Mathf.Min(speakerSamples, BUFFER_SIZE); i++)
            {
                speakerBuffer[speakerBufferIndex] = speakerData[i];
                speakerBufferIndex = (speakerBufferIndex + 1) % BUFFER_SIZE;
            }
        }

        private static float CalculateEchoLevel()
        {
            var totalEcho = 0f;
            var totalSpeaker = 0f;
            var totalMicrophone = 0f;

            for (var i = 0; i < BUFFER_SIZE; i++)
            {
                float speaker = speakerBuffer[i];
                float microphone = microphoneBuffer[i];
                float predictedEcho = speaker * echoPathGain * adaptiveFilterGain;

                totalSpeaker += speaker * speaker;
                totalMicrophone += microphone * microphone;
                totalEcho += predictedEcho * predictedEcho;
            }

            if (totalSpeaker > 0.001f && totalMicrophone > 0.001f)
            {
                float echoRatio = totalEcho / totalSpeaker;
                return Mathf.Sqrt(echoRatio);
            }

            return 0f;
        }

        private static float CalculateFrequencyDomainCorrelation()
        {
            if (FFT_SIZE > BUFFER_SIZE)
                return 0f;

            for (var i = 0; i < FFT_SIZE; i++)
            {
                fftBuffer[i * 2] = speakerBuffer[i];
                fftBuffer[(i * 2) + 1] = 0f;
            }

            FFT(fftBuffer, FFT_SIZE, false);

            for (var i = 0; i < FFT_SIZE; i++)
            {
                fftOutput[i * 2] = microphoneBuffer[i];
                fftOutput[(i * 2) + 1] = 0f;
            }

            FFT(fftOutput, FFT_SIZE, false);

            var correlation = 0f;
            var speakerEnergy = 0f;
            var microphoneEnergy = 0f;
            var crossEnergy = 0f;

            for (var i = 0; i < FFT_SIZE / 2; i++)
            {
                float speakerReal = fftBuffer[i * 2];
                float speakerImag = fftBuffer[(i * 2) + 1];
                float micReal = fftOutput[i * 2];
                float micImag = fftOutput[(i * 2) + 1];

                float speakerMag = Mathf.Sqrt((speakerReal * speakerReal) + (speakerImag * speakerImag));
                float micMag = Mathf.Sqrt((micReal * micReal) + (micImag * micImag));

                speakerEnergy += speakerMag * speakerMag;
                microphoneEnergy += micMag * micMag;
                crossEnergy += speakerMag * micMag;
            }

            if (speakerEnergy > 0.001f && microphoneEnergy > 0.001f) { correlation = crossEnergy / Mathf.Sqrt(speakerEnergy * microphoneEnergy); }

            return Mathf.Abs(correlation);
        }

        private static void UpdateEchoPathModel(float echoLevel, float correlation)
        {
            if (echoLevel > 0.1f && correlation > 0.2f)
            {
                float targetGain = echoLevel / (correlation + 0.1f);
                targetGain = Mathf.Clamp(targetGain, MIN_ECHO_PATH_GAIN, MAX_ECHO_PATH_GAIN);

                echoPathGain = Mathf.Lerp(echoPathGain, targetGain, ECHO_PATH_LEARNING_RATE);
                adaptiveFilterGain = Mathf.Lerp(adaptiveFilterGain, 1.0f / echoPathGain, ADAPTIVE_FILTER_LEARNING_RATE);
            }
            else if (correlation < 0.1f && echoLevel > 0.5f)
            {
                echoPathGain = Mathf.Lerp(echoPathGain, MIN_ECHO_PATH_GAIN, ECHO_PATH_LEARNING_RATE * 2f);
                adaptiveFilterGain = Mathf.Lerp(adaptiveFilterGain, 1.0f, ADAPTIVE_FILTER_LEARNING_RATE * 2f);
            }

            if (echoPathGain > MAX_ECHO_PATH_GAIN * 1.5f || echoPathGain < MIN_ECHO_PATH_GAIN * 0.5f)
            {
                echoPathGain = 1.0f;
                adaptiveFilterGain = 1.0f;
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Echo path gain out of bounds, resetting to 1.0");
            }
        }

        private static void FFT(float[] buffer, int size, bool inverse)
        {
            int n = size;
            var j = 0;

            for (var i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    float tempReal = buffer[i * 2];
                    float tempImag = buffer[(i * 2) + 1];
                    buffer[i * 2] = buffer[j * 2];
                    buffer[(i * 2) + 1] = buffer[(j * 2) + 1];
                    buffer[j * 2] = tempReal;
                    buffer[(j * 2) + 1] = tempImag;
                }

                int k = n >> 1;

                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }

                j += k;
            }

            for (var step = 1; step < n; step <<= 1)
            {
                float omega = (inverse ? 2.0f : -2.0f) * Mathf.PI / (step * 2);
                var wReal = 1.0f;
                var wImag = 0.0f;

                for (var group = 0; group < step; group++)
                {
                    for (int pair = group; pair < n; pair += step * 2)
                    {
                        int match = pair + step;
                        float productReal = (wReal * buffer[match * 2]) - (wImag * buffer[(match * 2) + 1]);
                        float productImag = (wReal * buffer[(match * 2) + 1]) + (wImag * buffer[match * 2]);

                        buffer[match * 2] = buffer[pair * 2] - productReal;
                        buffer[(match * 2) + 1] = buffer[(pair * 2) + 1] - productImag;
                        buffer[pair * 2] += productReal;
                        buffer[(pair * 2) + 1] += productImag;
                    }

                    float tempReal = wReal;
                    wReal = (tempReal * Mathf.Cos(omega)) - (wImag * Mathf.Sin(omega));
                    wImag = (tempReal * Mathf.Sin(omega)) + (wImag * Mathf.Cos(omega));
                }
            }

            if (inverse)
            {
                for (var i = 0; i < n * 2; i++) { buffer[i] /= n; }
            }
        }
    }
}
