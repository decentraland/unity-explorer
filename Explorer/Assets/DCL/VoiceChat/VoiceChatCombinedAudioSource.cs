using System;
using System.Collections.Generic;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;
using Utility.Multithreading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    public class VoiceChatCombinedAudioSource : MonoBehaviour
    {
        [field: SerializeField] private AudioSource audioSource;
        private readonly HashSet<WeakReference<IAudioStream>> streams = new ();
        private bool isPlaying;
        private int sampleRate = 48000; // Default to 48kHz for voice chat (LiveKit standard)
        private float[] tempBuffer;
        private int lastDataLength = 0; // Track buffer size changes

        // Audio debugging (counter-based, no time allocations)
        private int audioFrameCounter = 0;

        // Timing diagnostics for delay detection
        private System.Diagnostics.Stopwatch processingStopwatch = new();
        private float maxProcessingTimeMs = 0f;
        private int delayWarningCounter = 0;

        // Massive delay detection - track using frame counters (allocation-free)
        private bool wasReceivingAudio = false;
        private int audioStartFrame = 0;
        private int audioStopFrame = 0;
        private int consecutiveSilentFrames = 0;
        private int consecutiveAudioFrames = 0;

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            // Optimize buffer size for voice chat to reduce latency
            OptimizeAudioBufferForVoiceChat();
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        // Called by Unity on the Audio thread
        private void OnAudioFilterRead(float[] data, int channels)
        {
            processingStopwatch.Restart();
            audioFrameCounter++;

            if (!isPlaying || streams.Count == 0)
            {
                // Log every 300 frames when no audio (roughly every 6-7 seconds at 48kHz)
                if (audioFrameCounter % 300 == 0)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"[CombinedAudioSource] No audio streams - isPlaying: {isPlaying}, streams.Count: {streams.Count}");
                }
                Array.Clear(data, 0, data.Length);
                processingStopwatch.Stop();
                return;
            }

            // Only reallocate if buffer size actually changed (reduce allocation frequency)
            if (tempBuffer == null || lastDataLength != data.Length)
            {
                tempBuffer = new float[data.Length];
                lastDataLength = data.Length;
            }

            Array.Clear(data, 0, data.Length);
            var activeStreams = 0;
            float totalRms = 0f;
            bool shouldCalculateRms = audioFrameCounter % 180 == 0; // Only calculate RMS periodically

            foreach (WeakReference<IAudioStream> weakStream in streams)
            {
                if (weakStream.TryGetTarget(out IAudioStream stream))
                {
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);
                    stream.ReadAudio(tempBuffer, 1, sampleRate);

                    // Calculate RMS for this stream only when needed (for logging)
                    if (shouldCalculateRms)
                    {
                        float streamRms = 0f;
                        for (var i = 0; i < tempBuffer.Length; i++)
                            streamRms += tempBuffer[i] * tempBuffer[i];
                        streamRms = Mathf.Sqrt(streamRms / tempBuffer.Length);
                        totalRms += streamRms;
                    }

                    // Mix audio directly without temp storage
                    for (var i = 0; i < tempBuffer.Length; i++)
                        data[i] += tempBuffer[i];

                    activeStreams++;
                }
            }

            // Normalize only if multiple streams (avoid unnecessary computation)
            if (activeStreams > 1)
            {
                float norm = 1f / activeStreams;
                for (var i = 0; i < data.Length; i++)
                    data[i] *= norm;
            }

            processingStopwatch.Stop();
            float processingTimeMs = (float)processingStopwatch.Elapsed.TotalMilliseconds;

            // Track max processing time
            if (processingTimeMs > maxProcessingTimeMs)
                maxProcessingTimeMs = processingTimeMs;

            // Calculate expected frame time for this buffer size
            float expectedFrameTimeMs = (data.Length / channels) * 1000f / sampleRate;

            // Warn if processing is taking too long (potential cause of delay)
            if (processingTimeMs > expectedFrameTimeMs * 0.5f) // Using more than 50% of available time
            {
                delayWarningCounter++;
                if (delayWarningCounter % 10 == 1) // Log every 10th occurrence to avoid spam
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"[CombinedAudioSource] SLOW PROCESSING - Time: {processingTimeMs:F2}ms, " +
                        $"Expected: {expectedFrameTimeMs:F2}ms, Streams: {activeStreams}, " +
                        $"BufferSize: {data.Length}, Frame: {audioFrameCounter}");
                }
            }

            // Track massive delay issues using frame counting (allocation-free)
            bool hasAudioNow = activeStreams > 0 && totalRms > 0.0001f; // Very low threshold for any audio

            if (hasAudioNow)
            {
                consecutiveAudioFrames++;
                consecutiveSilentFrames = 0;

                if (!wasReceivingAudio)
                {
                    // Audio just started - calculate silence duration
                    audioStartFrame = audioFrameCounter;
                    if (audioStopFrame > 0)
                    {
                        int silentFrameCount = audioStartFrame - audioStopFrame;
                        float silenceDurationSeconds = (silentFrameCount * data.Length / channels) / (float)sampleRate;

                        if (silenceDurationSeconds > 10f) // More than 10 seconds silence = potential issue
                        {
                            ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                                $"[CombinedAudioSource] MASSIVE AUDIO GAP DETECTED - " +
                                $"Silent for {silenceDurationSeconds:F1}s ({silentFrameCount} frames), " +
                                $"StopFrame: {audioStopFrame}, StartFrame: {audioStartFrame}");
                        }
                    }
                    wasReceivingAudio = true;
                }
            }
            else
            {
                consecutiveSilentFrames++;
                consecutiveAudioFrames = 0;

                if (wasReceivingAudio)
                {
                    // Audio just stopped
                    audioStopFrame = audioFrameCounter;
                    wasReceivingAudio = false;

                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"[CombinedAudioSource] Audio stopped - StopFrame: {audioStopFrame}, " +
                        $"ActiveStreams: {activeStreams}, TotalStreams: {streams.Count}");
                }

                // Warn about extended silence (potential delay issue)
                if (consecutiveSilentFrames == 1200) // ~25 seconds at 48kHz
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"[CombinedAudioSource] EXTENDED SILENCE - No audio for 25+ seconds, " +
                        $"Frame: {audioFrameCounter}, ActiveStreams: {activeStreams}, " +
                        $"TotalStreams: {streams.Count}. Check LiveKit connection.");
                }
            }

            // Throttled logging every 180 frames (roughly every 3-4 seconds)
            if (shouldCalculateRms && activeStreams > 0 && hasAudioNow)
            {
                float dbLevel = totalRms > 0f ? 20f * Mathf.Log10(totalRms) : -100f;
                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"[CombinedAudioSource] Audio Stats - ActiveStreams: {activeStreams}, " +
                    $"TotalStreams: {streams.Count}, RMS: {totalRms:F4} ({dbLevel:F1} dB), " +
                    $"SampleRate: {sampleRate}Hz, Channels: {channels}, " +
                    $"ProcessTime: {processingTimeMs:F2}ms, MaxProcessTime: {maxProcessingTimeMs:F2}ms, " +
                    $"BufferSize: {data.Length} samples ({expectedFrameTimeMs:F1}ms), Frame: {audioFrameCounter}, " +
                    $"ConsecutiveAudio: {consecutiveAudioFrames}, ConsecutiveSilent: {consecutiveSilentFrames}");

                // Reset max processing time after logging
                maxProcessingTimeMs = 0f;
            }
        }

        public void AddStream(WeakReference<IAudioStream> stream)
        {
            streams.Add(stream);
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"[CombinedAudioSource] Stream added - Total streams: {streams.Count}");
        }

        public void RemoveStream(WeakReference<IAudioStream> stream)
        {
            streams.Remove(stream);
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"[CombinedAudioSource] Stream removed - Total streams: {streams.Count}");
        }

        public void Free()
        {
            streams.Clear();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "[CombinedAudioSource] All streams cleared");
        }

        public void Play()
        {
            isPlaying = true;

            if (!PlayerLoopHelper.IsMainThread)
            {
                PlayAsync().Forget();
                return;
            }

            audioSource.Play();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "[CombinedAudioSource] Started playing");
        }

        private async UniTaskVoid PlayAsync()
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();
            audioSource.Play();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "[CombinedAudioSource] Started playing (async)");
        }

        public void Stop()
        {
            isPlaying = false;

            if (!PlayerLoopHelper.IsMainThread)
            {
                StopAsync().Forget();
                return;
            }

            audioSource.Stop();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "[CombinedAudioSource] Stopped playing");
        }

        private async UniTaskVoid StopAsync()
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();
            audioSource.Stop();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "[CombinedAudioSource] Stopped playing (async)");
        }

        public void SetVolume(float target)
        {
            audioSource.volume = target;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            sampleRate = AudioSettings.outputSampleRate;
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"[CombinedAudioSource] Audio config changed - SampleRate: {sampleRate}Hz, DeviceChanged: {deviceWasChanged}");
        }

        private void OptimizeAudioBufferForVoiceChat()
        {
            var currentConfig = AudioSettings.GetConfiguration();
            int currentBufferSize = currentConfig.dspBufferSize;
            int currentSampleRate = currentConfig.sampleRate;

            // Calculate current latency in milliseconds
            float currentLatencyMs = (currentBufferSize * 1000f) / currentSampleRate;

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"[CombinedAudioSource] Current audio config - BufferSize: {currentBufferSize}, " +
                $"SampleRate: {currentSampleRate}Hz, Latency: {currentLatencyMs:F1}ms");

            // For voice chat, aim for 256 samples buffer (5.3ms latency at 48kHz)
            // This is a good balance between latency and stability
            int targetBufferSize = 256;

            if (currentBufferSize > targetBufferSize)
            {
                var newConfig = currentConfig;
                newConfig.dspBufferSize = targetBufferSize;

                if (AudioSettings.Reset(newConfig))
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"[CombinedAudioSource] Audio buffer optimized for voice chat - " +
                        $"BufferSize: {currentBufferSize} → {targetBufferSize}, " +
                        $"Latency: {currentLatencyMs:F1}ms → {(targetBufferSize * 1000f) / currentSampleRate:F1}ms");
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"[CombinedAudioSource] Failed to optimize audio buffer - " +
                        $"platform/driver may not support BufferSize: {targetBufferSize}. " +
                        $"Current latency: {currentLatencyMs:F1}ms may affect voice chat responsiveness");
                }
            }
            else
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"[CombinedAudioSource] Audio buffer already optimized - " +
                    $"BufferSize: {currentBufferSize}, Latency: {currentLatencyMs:F1}ms");
            }
        }
    }
}
