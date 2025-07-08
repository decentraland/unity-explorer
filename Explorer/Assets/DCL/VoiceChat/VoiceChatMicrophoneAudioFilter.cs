using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using Utility.Multithreading;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that applies real-time noise reduction and audio processing
    ///     to the microphone input using Unity's OnAudioFilterRead callback.
    ///     Compatible with LiveKit's AudioFilter interface.
    ///     Processing and resampling are done in a separate thread to avoid audio dropouts.
    /// </summary>
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private const int DEFAULT_LIVEKIT_CHANNELS = 1;
        private const int DEFAULT_BUFFER_SIZE = 8192;
        private const int LIVEKIT_FRAME_SIZE = 960; // 20ms at 48kHz
        private const int PROCESSING_QUEUE_SIZE = 10; // Buffer for processing thread

        private IVoiceChatAudioProcessor audioProcessor;
        private bool isFilterActive = true;

        private readonly List<float> liveKitBuffer = new (LIVEKIT_FRAME_SIZE * 2);
        private int outputSampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
        private float[] resampleBuffer;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;
        private bool isProcessingEnabled => voiceChatConfiguration != null && voiceChatConfiguration.EnableAudioProcessing;

        private Thread processingThread;
        private readonly ConcurrentQueue<AudioProcessingJob> processingQueue = new();
        private readonly ConcurrentQueue<ProcessedAudioData> processedQueue = new();
        private volatile bool shouldStopProcessing;
        private readonly ManualResetEvent processingEvent = new(false);
        private CancellationTokenSource processingCancellationTokenSource;

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
            audioProcessor = null;
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

            if (audioProcessor != null && data.Length > 0)
            {
                SubmitAudioForProcessing(data, channels, outputSampleRate, samplesPerChannel);

                if (processedQueue.TryDequeue(out ProcessedAudioData processedData))
                {
                    if (tempBuffer == null || tempBuffer.Length < processedData.ProcessedData.Length)
                        tempBuffer = new float[Mathf.Max(processedData.ProcessedData.Length, DEFAULT_BUFFER_SIZE)];

                    processedData.ProcessedData.CopyTo(tempBuffer, 0);
                    sendBuffer = tempBuffer.AsSpan(0, processedData.ProcessedData.Length);
                    samplesPerChannel = processedData.SamplesPerChannel;
                }
                else
                {
                    // Thread is behind - drop this frame
                    return;
                }
            }

            // Buffer the audio for LiveKit
            for (var i = 0; i < samplesPerChannel; i++)
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

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void SubmitAudioForProcessing(float[] audioData, int channels, int sampleRate, int samplesPerChannel)
        {
            if (processingQueue.Count >= PROCESSING_QUEUE_SIZE)
            {
                // Drop the oldest job if the queue is full to prevent memory buildup
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
            Span<float> monoSpan = localTempBuffer.AsSpan(0, samplesPerChannel);

            VoiceChatMicrophoneAudioHelpers.ConvertToMono(data, monoSpan, job.Channels, samplesPerChannel);

            if (isProcessingEnabled)
            {
                audioProcessor.ProcessAudio(monoSpan, job.SampleRate);
            }

            if (outputSampleRate != VoiceChatConstants.LIVEKIT_SAMPLE_RATE)
            {
                var targetSamplesPerChannel = (int)((float)samplesPerChannel * VoiceChatConstants.LIVEKIT_SAMPLE_RATE / outputSampleRate);
                Span<float> resampledSpan = localTempBuffer.AsSpan(samplesPerChannel, targetSamplesPerChannel);
                
                VoiceChatMicrophoneAudioHelpers.ResampleCubic(
                    monoSpan.Slice(0, samplesPerChannel), 
                    outputSampleRate, 
                    resampledSpan, 
                    VoiceChatConstants.LIVEKIT_SAMPLE_RATE);

                var result = new float[targetSamplesPerChannel];
                resampledSpan.CopyTo(result);

                return new ProcessedAudioData
                {
                    ProcessedData = result,
                    SamplesPerChannel = targetSamplesPerChannel
                };
            }

            var monoResult = new float[samplesPerChannel];
            monoSpan.Slice(0, samplesPerChannel).CopyTo(monoResult);

            return new ProcessedAudioData
            {
                ProcessedData = monoResult,
                SamplesPerChannel = samplesPerChannel
            };
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            outputSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
            audioProcessor = new OptimizedVoiceChatAudioProcessor(configuration);
        }

        public void SetFilterActive(bool active)
        {
            isFilterActive = active;
            audioProcessor?.Reset();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);

            // Clear processing queues when filter is reset
            while (processingQueue.TryDequeue(out _)) { }
            while (processedQueue.TryDequeue(out _)) { }
        }

        public void Reset()
        {
            liveKitBuffer.Clear();

            if (tempBuffer != null)
                Array.Clear(tempBuffer, 0, tempBuffer.Length);

            if (resampleBuffer != null)
                Array.Clear(resampleBuffer, 0, resampleBuffer.Length);

            while (processingQueue.TryDequeue(out _)) { }
            while (processedQueue.TryDequeue(out _)) { }

            audioProcessor?.Reset();
        }
    }
}
