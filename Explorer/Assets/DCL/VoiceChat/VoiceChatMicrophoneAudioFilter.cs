using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using Utility.Multithreading;
using Utility;
using ThreadPriority = System.Threading.ThreadPriority;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that applies resampling and volume increase to microphone input
    ///     using Unity's OnAudioFilterRead callback. Compatible with LiveKit's AudioFilter interface.
    ///     Resampling is done in a separate thread to avoid audio dropouts.
    /// </summary>
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private const int DEFAULT_LIVEKIT_CHANNELS = 2;
        private const int DEFAULT_BUFFER_SIZE = 8192;
        private const int LIVEKIT_FRAME_SIZE = 240; // 5ms at 48kHz (stereo samples)
        private const int PROCESSING_QUEUE_SIZE = 10; // Buffer for processing thread

        private readonly List<float> liveKitBuffer = new (LIVEKIT_FRAME_SIZE * 2); // Buffer for stereo data
        private readonly ConcurrentQueue<ProcessedAudioData> processedQueue = new ();
        private readonly ManualResetEvent processingEvent = new (false);
        private readonly ConcurrentQueue<AudioProcessingJob> processingQueue = new ();

        private bool isFilterActive = true;
        private int outputSampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
        private CancellationTokenSource processingCancellationTokenSource;

        private Thread processingThread;
        private float[] resampleBuffer;
        private volatile bool shouldStopProcessing;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        public void Reset()
        {
            liveKitBuffer.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);

            while (processingQueue.TryDequeue(out _)) { }

            while (processedQueue.TryDequeue(out _)) { }
        }

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            StartProcessingThread();
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            StopProcessingThread();

            liveKitBuffer.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);

            outputSampleRate = AudioSettings.outputSampleRate;
        }

        private void OnDestroy()
        {
            AudioRead = null!;
            StopProcessingThread();
            tempBuffer = null;
            resampleBuffer = null;
        }

        /// <summary>
        ///     Unity's audio filter callback
        ///     Handles buffering and sending to LiveKit, processing is done in separate thread
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isFilterActive || data == null)
                return;

            int samplesPerChannel = data.Length / channels;
            Span<float> sendBuffer = data;

            if (data.Length > 0)
            {
                SubmitAudioForProcessing(data, samplesPerChannel);

                if (processedQueue.TryDequeue(out ProcessedAudioData processedData))
                {
                    if (tempBuffer == null || tempBuffer.Length < processedData.ProcessedData.Length)
                        tempBuffer = new float[Mathf.Max(processedData.ProcessedData.Length, DEFAULT_BUFFER_SIZE)];

                    processedData.ProcessedData.CopyTo(tempBuffer, 0);
                    sendBuffer = tempBuffer.AsSpan(0, processedData.ProcessedData.Length);
                }
                else
                {
                    // Thread is behind - drop this frame
                    return;
                }
            }

            // Buffer the audio for LiveKit
            // For stereo data, we buffer all samples (not just samplesPerChannel)
            for (var i = 0; i < sendBuffer.Length; i++)
                liveKitBuffer.Add(sendBuffer[i]);

            while (liveKitBuffer.Count >= LIVEKIT_FRAME_SIZE)
            {
                if (tempBuffer == null || tempBuffer.Length < LIVEKIT_FRAME_SIZE)
                    tempBuffer = new float[LIVEKIT_FRAME_SIZE];

                liveKitBuffer.CopyTo(0, tempBuffer, 0, LIVEKIT_FRAME_SIZE);
                AudioRead?.Invoke(tempBuffer.AsSpan(0, LIVEKIT_FRAME_SIZE), DEFAULT_LIVEKIT_CHANNELS, VoiceChatConstants.LIVEKIT_SAMPLE_RATE);
                liveKitBuffer.RemoveRange(0, LIVEKIT_FRAME_SIZE);
            }
        }

        /// <summary>
        ///     Event called from the Unity audio thread when audio data is available
        /// </summary>

        // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
        public event IAudioFilter.OnAudioDelegate AudioRead;

        public bool IsValid => voiceChatConfiguration != null;

        private void SubmitAudioForProcessing(float[] audioData, int samplesPerChannel)
        {
            if (processingQueue.Count >= PROCESSING_QUEUE_SIZE)
            {
                // Drop the oldest job if the queue is full to prevent memory buildup
                processingQueue.TryDequeue(out _);
            }

            var job = new AudioProcessingJob
            {
                AudioData = new float[audioData.Length],
                SamplesPerChannel = samplesPerChannel,
            };

            audioData.CopyTo(job.AudioData, 0);
            processingQueue.Enqueue(job);
            processingEvent.Set();
        }

        private void StartProcessingThread()
        {
            if (processingThread is { IsAlive: true })
                return;

            shouldStopProcessing = false;
            processingCancellationTokenSource = new CancellationTokenSource();

            processingThread = new Thread(ProcessingThreadWorker)
            {
                Name = "VoiceChatAudioProcessor",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };

            processingThread.Start();
        }

        private void StopProcessingThread()
        {
            if (processingThread == null || !processingThread.IsAlive)
                return;

            shouldStopProcessing = true;
            processingCancellationTokenSource.SafeCancelAndDispose();
            processingEvent.Set();

            if (processingThread.Join(1000)) { ReportHub.Log(ReportCategory.VOICE_CHAT, "Audio processing thread stopped successfully"); }
            else { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Audio processing thread did not stop gracefully"); }

            // Clear queues
            while (processingQueue.TryDequeue(out _)) { }

            while (processedQueue.TryDequeue(out _)) { }

            processingCancellationTokenSource = null;
            processingThread = null;
        }

        private void ProcessingThreadWorker()
        {
            var localTempBuffer = new float[DEFAULT_BUFFER_SIZE * 2];

            while (!shouldStopProcessing)
            {
                try
                {
                    // Wait for work with timeout and respect cancellation
                    if (processingEvent.WaitOne(100))
                    {
                        MultithreadingUtility.WaitWhileOnPause();

                        while (processingQueue.TryDequeue(out AudioProcessingJob job) && !shouldStopProcessing)
                        {
                            try
                            {
                                ProcessedAudioData processedData = ProcessAudioDataThreaded(job, localTempBuffer);
                                processedQueue.Enqueue(processedData);
                            }
                            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Threaded audio processing error: {ex.Message}"); }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, exit the loop
                    break;
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Processing thread error: {ex.Message}");

                    // Continue processing unless we should stop
                    if (shouldStopProcessing) break;
                }
            }
        }

        private ProcessedAudioData ProcessAudioDataThreaded(AudioProcessingJob job, float[] localTempBuffer)
        {
            int samplesPerChannel = job.SamplesPerChannel;
            Span<float> data = job.AudioData.AsSpan();

            Span<float> stereoSpan = localTempBuffer.AsSpan(0, data.Length);

            float volumeMultiplier = voiceChatConfiguration.MicrophoneVolume;
            for (var i = 0; i < data.Length; i++)
            {
                stereoSpan[i] = data[i] * volumeMultiplier;
            }

            if (outputSampleRate != VoiceChatConstants.LIVEKIT_SAMPLE_RATE)
            {
                var targetSamplesPerChannel = (int)((float)samplesPerChannel * VoiceChatConstants.LIVEKIT_SAMPLE_RATE / outputSampleRate);
                int targetTotalSamples = targetSamplesPerChannel * 2;
                Span<float> resampledSpan = localTempBuffer.AsSpan(data.Length, targetTotalSamples);

                VoiceChatMicrophoneAudioHelpers.ResampleStereo(
                    stereoSpan,
                    outputSampleRate,
                    resampledSpan,
                    VoiceChatConstants.LIVEKIT_SAMPLE_RATE);

                var result = new float[targetTotalSamples];
                resampledSpan.CopyTo(result);

                return new ProcessedAudioData
                {
                    ProcessedData = result,
                };
            }

            var stereoResult = new float[data.Length];
            stereoSpan.CopyTo(stereoResult);

            return new ProcessedAudioData
            {
                ProcessedData = stereoResult,
            };
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);

            // Clear processing queues when filter is reset
            while (processingQueue.TryDequeue(out _)) { }

            while (processedQueue.TryDequeue(out _)) { }
        }

        private struct AudioProcessingJob
        {
            public float[] AudioData;
            public int SamplesPerChannel;
        }

        private struct ProcessedAudioData
        {
            public float[] ProcessedData;
        }
    }
}
