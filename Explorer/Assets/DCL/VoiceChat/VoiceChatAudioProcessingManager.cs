using DCL.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Manages audio processing in a separate thread to avoid audio dropouts.
    ///     Handles job queuing, processing coordination, and thread lifecycle.
    /// </summary>
    public class VoiceChatAudioProcessingManager : IDisposable
    {
        private const int DEFAULT_BUFFER_SIZE = 8192;
        private const int PROCESSING_QUEUE_SIZE = 10;

        private readonly IVoiceChatAudioProcessor audioProcessor;
        private readonly VoiceChatConfiguration configuration;
        private readonly ConcurrentQueue<AudioProcessingJob> processingQueue = new();
        private readonly ConcurrentQueue<ProcessedAudioData> processedQueue = new();
        private readonly ManualResetEvent processingEvent = new(false);

        private Thread processingThread;
        private volatile bool shouldStopProcessing;
        private CancellationTokenSource processingCancellationTokenSource;
        private bool isDisposed;

        public bool IsProcessingEnabled => configuration?.EnableAudioProcessing == true;

        private struct AudioProcessingJob
        {
            public float[] AudioData;
            public int Channels;
            public int SampleRate;
            public int SamplesPerChannel;
        }

        private struct ProcessedAudioData
        {
            public float[] ProcessedData;
            public int SamplesPerChannel;
        }

        public VoiceChatAudioProcessingManager(IVoiceChatAudioProcessor audioProcessor, VoiceChatConfiguration configuration)
        {
            this.audioProcessor = audioProcessor ?? throw new ArgumentNullException(nameof(audioProcessor));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            StartProcessingThread();
        }

        public int ProcessAudio(float[] audioData, int channels, int sampleRate, int samplesPerChannel, Span<float> outputBuffer)
        {
            if (!IsProcessingEnabled || audioProcessor == null)
            {
                var monoBuffer = new float[samplesPerChannel];
                VoiceChatAudioFormatConverter.ConvertToMono(audioData.AsSpan(), channels, monoBuffer.AsSpan(), samplesPerChannel);
                monoBuffer.AsSpan().CopyTo(outputBuffer);
                return samplesPerChannel;
            }

            SubmitAudioForProcessing(audioData, channels, sampleRate, samplesPerChannel);

            if (processedQueue.TryDequeue(out ProcessedAudioData processedData))
            {
                processedData.ProcessedData.AsSpan().CopyTo(outputBuffer);
                return processedData.SamplesPerChannel;
            }

            var fallbackBuffer = new float[samplesPerChannel];
            VoiceChatAudioFormatConverter.ConvertToMono(audioData.AsSpan(), channels, fallbackBuffer.AsSpan(), samplesPerChannel);
            fallbackBuffer.AsSpan().CopyTo(outputBuffer);
            return samplesPerChannel;
        }

        private void SubmitAudioForProcessing(float[] audioData, int channels, int sampleRate, int samplesPerChannel)
        {
            if (processingQueue.Count >= PROCESSING_QUEUE_SIZE)
            {
                processingQueue.TryDequeue(out _);
            }

            var job = new AudioProcessingJob
            {
                AudioData = new float[audioData.Length],
                Channels = channels,
                SampleRate = sampleRate,
                SamplesPerChannel = samplesPerChannel
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
                Priority = System.Threading.ThreadPriority.AboveNormal
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

            if (processingThread.Join(1000))
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Audio processing thread stopped successfully");
            }
            else
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Audio processing thread did not stop gracefully");
            }

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
                                var processedData = ProcessAudioDataThreaded(job, localTempBuffer);
                                processedQueue.Enqueue(processedData);
                            }
                            catch (Exception ex)
                            {
                                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Threaded audio processing error: {ex.Message}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Processing thread error: {ex.Message}");
                    if (shouldStopProcessing) break;
                }
            }
        }

        private ProcessedAudioData ProcessAudioDataThreaded(AudioProcessingJob job, float[] localTempBuffer)
        {
            int samplesPerChannel = job.SamplesPerChannel;
            Span<float> data = job.AudioData.AsSpan();
            Span<float> monoSpan = localTempBuffer.AsSpan(0, samplesPerChannel);

            VoiceChatAudioFormatConverter.ConvertToMono(data, job.Channels, monoSpan, samplesPerChannel);

            audioProcessor.ProcessAudio(monoSpan, job.SampleRate);

            var liveKitBufferSize = VoiceChatAudioFormatConverter.CalculateLiveKitBufferSize(samplesPerChannel, job.SampleRate);
            var liveKitBuffer = new float[liveKitBufferSize];
            var outputSamples = VoiceChatAudioFormatConverter.ConvertToLiveKitFormat(monoSpan, job.SampleRate, liveKitBuffer.AsSpan());

            return new ProcessedAudioData
            {
                ProcessedData = liveKitBuffer,
                SamplesPerChannel = outputSamples
            };
        }

        public void Reset()
        {
            audioProcessor?.Reset();
            
            while (processingQueue.TryDequeue(out _)) { }
            while (processedQueue.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            
            StopProcessingThread();
            processingEvent?.Dispose();
            isDisposed = true;
        }
    }
} 