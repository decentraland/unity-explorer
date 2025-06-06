using DCL.Diagnostics;
using UnityEngine;
using LiveKit;
using LiveKit.Internal;
using LiveKit.Rooms.Streaming.Audio;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Utility;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Custom AudioFilter that applies real-time noise reduction and audio processing
    ///     to the microphone input using Unity's OnAudioFilterRead callback.
    ///     Compatible with LiveKit's AudioFilter interface.
    /// </summary>
    public class VoiceChatMicrophoneAudioFilter : MonoBehaviour, IAudioFilter
    {
        private VoiceChatAudioProcessor audioProcessor;

        private bool processingEnabled = true;
        private int cachedSampleRate;
        private bool isProcessingEnabled = true; // Used for macOS to disable processing if exceptions occur, cannot be readonly
        private float[] silenceBuffer;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        private IAudioFilter.OnAudioDelegate audioReadEvent;

        private int lastMicrophonePosition = 0;
        private int microphoneSampleRate = 48000;
        private int microphoneBufferLengthSeconds = 1;
        private string currentMicrophoneName;
        private bool useMicrophonePositionOptimization = true;

        private AudioClip microphoneClip;
        private float[] realtimeAudioBuffer;
        private float[] processingSamplesBuffer;
        private const int REALTIME_CHUNK_SIZE = 256;
        private bool useRealtimeProcessing = true;

        private CancellationTokenSource realtimeProcessingCts;
        private bool isRealtimeThreadRunning = false;
        private volatile int currentMicrophonePosition = 0;

        private readonly Queue<float[]> audioDataQueue = new Queue<float[]>();
        private readonly object queueLock = new object();

        private void Awake()
        {
            if (voiceChatConfiguration != null)
            {
                audioProcessor = new VoiceChatAudioProcessor(voiceChatConfiguration);
            }
        }

        private void Start()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (tempBuffer == null)
                tempBuffer = new float[8192];
#endif
        }

        private void Update()
        {
            if (processingEnabled && useRealtimeProcessing && !string.IsNullOrEmpty(currentMicrophoneName))
            {
                ProcessRealtimeMicrophoneInput();
            }
        }

        /// <summary>
        /// Process microphone input in real-time from main thread
        /// This method reads audio data from microphone and queues it for background processing
        /// </summary>
        private void ProcessRealtimeMicrophoneInput()
        {
            if (microphoneClip == null)
                return;

            currentMicrophonePosition = Microphone.GetPosition(currentMicrophoneName);

            if (currentMicrophonePosition == lastMicrophonePosition)
                return;

            int totalBufferSamples = microphoneClip.samples;
            int samplesToRead;

            if (currentMicrophonePosition >= lastMicrophonePosition)
            {
                samplesToRead = currentMicrophonePosition - lastMicrophonePosition;
            }
            else
            {
                samplesToRead = (totalBufferSamples - lastMicrophonePosition) + currentMicrophonePosition;
            }

            while (samplesToRead > 0)
            {
                int chunkSize = Mathf.Min(samplesToRead, REALTIME_CHUNK_SIZE);

                if (realtimeAudioBuffer == null || realtimeAudioBuffer.Length < chunkSize)
                {
                    realtimeAudioBuffer = new float[Mathf.Max(chunkSize, REALTIME_CHUNK_SIZE)];
                }

                Span<float> audioSpan = realtimeAudioBuffer.AsSpan(0, chunkSize);
                bool success = microphoneClip.GetData(audioSpan, lastMicrophonePosition);

                if (!success)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"Failed to get microphone data at position {lastMicrophonePosition}, chunkSize: {chunkSize}");
                    break;
                }

                float[] audioChunk = new float[chunkSize];
                audioSpan.CopyTo(audioChunk.AsSpan());

                lock (queueLock)
                {
                    audioDataQueue.Enqueue(audioChunk);
                }

                lastMicrophonePosition = (lastMicrophonePosition + chunkSize) % totalBufferSamples;
                samplesToRead -= chunkSize;
            }
        }

        private void OnEnable()
        {
            processingEnabled = true;
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            StartRealtimeProcessingThread();
        }

        private void OnDisable()
        {
            processingEnabled = false;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;

            StopRealtimeProcessingThread();

            lock (queueLock)
            {
                audioDataQueue.Clear();
            }
        }

        private void OnDestroy()
        {
            StopRealtimeProcessingThread();
            ClearAudioReadSubscribers();
            audioProcessor = null;
            tempBuffer = null;
            silenceBuffer = null;
        }

        /// <summary>
        ///     Unity's audio filter callback - serves as fallback when real-time processing isn't available
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null)
                return;

            if (!processingEnabled)
            {
                Span<float> silenceSpan = GetSilenceSpan(data.Length / channels);
                audioReadEvent?.Invoke(silenceSpan, 1, GetEffectiveSampleRate());
                return;
            }

            if (useRealtimeProcessing && useMicrophonePositionOptimization && !string.IsNullOrEmpty(currentMicrophoneName) && isRealtimeThreadRunning)
            {
                return;
            }

            if (isProcessingEnabled && audioProcessor != null && voiceChatConfiguration != null && data.Length > 0)
            {
                try
                {
                    Span<float> processedSpan = ProcessAudioToSpan(data.AsSpan(), channels);
                    audioReadEvent?.Invoke(processedSpan, 1, GetEffectiveSampleRate());
                    return;
                }
                catch (Exception ex)
                {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error on macOS: {ex.Message}. Disabling processing to prevent audio system instability.");
                    isProcessingEnabled = false;
#else
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error: {ex.Message}");
#endif
                }
            }

            Span<float> monoSpan = ConvertToMonoSpan(data.AsSpan(), channels);
            audioReadEvent?.Invoke(monoSpan, 1, GetEffectiveSampleRate());
        }

        public event IAudioFilter.OnAudioDelegate AudioRead
        {
            add
            {
                audioReadEvent += value;
            }
            remove
            {
                audioReadEvent -= value;
            }
        }

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void ClearAudioReadSubscribers()
        {
            audioReadEvent = null;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            cachedSampleRate = AudioSettings.outputSampleRate;
        }

        public void Initialize(VoiceChatConfiguration configuration)
        {
            voiceChatConfiguration = configuration;
            audioProcessor = new VoiceChatAudioProcessor(configuration);
        }

        /// <summary>
        /// Enable or disable audio processing without clearing LiveKit subscribers.
        /// Use this for muting/unmuting instead of disabling the component.
        /// </summary>
        public void SetProcessingEnabled(bool enabled)
        {
            processingEnabled = enabled;
        }

        public void SetMicrophoneInfo(string microphoneName, int sampleRate, int bufferLengthSeconds)
        {
            currentMicrophoneName = microphoneName;
            microphoneSampleRate = sampleRate;
            microphoneBufferLengthSeconds = bufferLengthSeconds;
            lastMicrophonePosition = 0;

            realtimeAudioBuffer = new float[REALTIME_CHUNK_SIZE];
            processingSamplesBuffer = new float[REALTIME_CHUNK_SIZE];
        }

        public void SetMicrophoneClip(AudioClip clip)
        {
            microphoneClip = clip;
        }

        public void ResetProcessor()
        {
            audioProcessor?.Reset();
        }

        /// <summary>
        /// Start background thread for truly real-time audio processing
        /// </summary>
        private void StartRealtimeProcessingThread()
        {
            if (isRealtimeThreadRunning)
                return;

            realtimeProcessingCts = new CancellationTokenSource();
            isRealtimeThreadRunning = true;

            RealtimeAudioProcessingLoopAsync(realtimeProcessingCts.Token).Forget();
        }

        /// <summary>
        /// Stop background thread for real-time audio processing
        /// </summary>
        private void StopRealtimeProcessingThread()
        {
            if (!isRealtimeThreadRunning)
                return;

            isRealtimeThreadRunning = false;
            realtimeProcessingCts?.SafeCancelAndDispose();
            realtimeProcessingCts = null;
        }

        /// <summary>
        /// Background thread loop for truly real-time audio processing
        /// Processes audio data queued from the main thread
        /// </summary>
        private async UniTaskVoid RealtimeAudioProcessingLoopAsync(CancellationToken ct)
        {
            await UniTask.SwitchToThreadPool();

            try
            {
                const int POLL_INTERVAL_MS = 1;

                bool initialConditionsOk = !ct.IsCancellationRequested && useRealtimeProcessing;
                if (!initialConditionsOk)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"Real-time processing loop cannot start - ct.IsCancellationRequested: {ct.IsCancellationRequested}, useRealtimeProcessing: {useRealtimeProcessing}");
                    return;
                }

                while (!ct.IsCancellationRequested && useRealtimeProcessing)
                {
                    try
                    {
                        if (processingEnabled)
                        {
                            while (true)
                            {
                                float[] audioChunk = null;

                                lock (queueLock)
                                {
                                    if (audioDataQueue.Count > 0)
                                    {
                                        audioChunk = audioDataQueue.Dequeue();
                                    }
                                }

                                if (audioChunk == null)
                                    break;

                                ProcessRealtimeAudioChunk(audioChunk, audioChunk.Length);
                            }
                        }
                        else
                        {
                            lock (queueLock)
                            {
                                audioDataQueue.Clear();
                            }
                        }

                        await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (System.Exception ex)
                    {
                        ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                            $"Real-time audio processing error: {ex.Message}. Continuing...");

                        await UniTask.Delay(10, cancellationToken: ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected cancellation
            }
            catch (System.Exception ex)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT,
                    $"Real-time audio processing thread failed: {ex.Message}. Falling back to standard processing.");
                useRealtimeProcessing = false;
            }
            finally
            {
                isRealtimeThreadRunning = false;
            }
        }

        /// <summary>
        /// Process a chunk of real-time audio data with minimal latency
        /// </summary>
        private void ProcessRealtimeAudioChunk(float[] audioChunk, int sampleCount)
        {
            if (!processingEnabled || audioProcessor == null || voiceChatConfiguration == null)
                return;

            try
            {
                Span<float> audioSpan = audioChunk.AsSpan(0, sampleCount);
                audioProcessor.ProcessAudio(audioSpan, microphoneSampleRate);

                audioReadEvent?.Invoke(audioSpan, 1, microphoneSampleRate);
            }
            catch (Exception ex)
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Real-time audio processing error on macOS: {ex.Message}. Disabling real-time processing.");
                useRealtimeProcessing = false;
#else
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Real-time audio processing error: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// Get the effective sample rate to use for audio processing
        /// Prioritizes microphone sample rate over Unity's output sample rate
        /// </summary>
        private int GetEffectiveSampleRate()
        {
            return microphoneSampleRate > 0 ? microphoneSampleRate : cachedSampleRate;
        }

        private void ConvertToMono(ReadOnlySpan<float> inputData, Span<float> outputBuffer, int channels)
        {
            if (channels == 1)
            {
                inputData.CopyTo(outputBuffer);
                return;
            }

            int samplesPerChannel = inputData.Length / channels;
            for (var sampleIndex = 0; sampleIndex < samplesPerChannel; sampleIndex++)
            {
                var sum = 0f;
                int baseIndex = sampleIndex * channels;

                for (var ch = 0; ch < channels; ch++)
                {
                    sum += inputData[baseIndex + ch];
                }

                outputBuffer[sampleIndex] = sum / channels;
            }
        }

        private Span<float> GetSilenceSpan(int length)
        {
            if (silenceBuffer == null || silenceBuffer.Length < length)
            {
                silenceBuffer = new float[Mathf.Max(length, 1024)];
            }

            Span<float> bufferSpan = silenceBuffer.AsSpan(0, length);
            bufferSpan.Clear();

            return bufferSpan;
        }

        private Span<float> ProcessAudioToSpan(ReadOnlySpan<float> data, int channels)
        {
            Span<float> workingSpan;
            if (channels == 1)
            {
                if (tempBuffer == null || tempBuffer.Length < data.Length)
                {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                    tempBuffer = new float[Mathf.Max(data.Length, 8192)];
#else
                    tempBuffer = new float[data.Length];
#endif
                }
                workingSpan = tempBuffer.AsSpan(0, data.Length);
                data.CopyTo(workingSpan);
            }
            else
            {
                int samplesPerChannel = data.Length / channels;

                if (tempBuffer == null || tempBuffer.Length < samplesPerChannel)
                {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                    tempBuffer = new float[Mathf.Max(samplesPerChannel, 8192)];
#else
                    tempBuffer = new float[samplesPerChannel];
#endif
                }

                workingSpan = tempBuffer.AsSpan(0, samplesPerChannel);
                ConvertToMono(data, workingSpan, channels);
            }

            audioProcessor.ProcessAudio(workingSpan, GetEffectiveSampleRate());

            return workingSpan;
        }

        private Span<float> ConvertToMonoSpan(ReadOnlySpan<float> data, int channels)
        {
            if (channels == 1)
            {
                if (tempBuffer == null || tempBuffer.Length < data.Length)
                {
                    tempBuffer = new float[data.Length];
                }
                Span<float> monoSpan = tempBuffer.AsSpan(0, data.Length);
                data.CopyTo(monoSpan);
                return monoSpan;
            }

            int samplesPerChannel = data.Length / channels;

            if (tempBuffer == null || tempBuffer.Length < samplesPerChannel)
            {
                tempBuffer = new float[samplesPerChannel];
            }

            Span<float> outputSpan = tempBuffer.AsSpan(0, samplesPerChannel);
            ConvertToMono(data, outputSpan, channels);

            return outputSpan;
        }
    }
}
