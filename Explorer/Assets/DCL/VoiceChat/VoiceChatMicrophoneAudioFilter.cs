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

        private bool cachedEnabled = true; // Cache enabled state for audio thread access
        private int cachedSampleRate; // Unity's output sample rate
        private readonly bool isProcessingEnabled = true; //Used for macOS to disable processing if exceptions occur, cannot be readonly
        private float[] silenceBuffer;

        private float[] tempBuffer;
        private VoiceChatConfiguration voiceChatConfiguration;

        // Explicit backing field for efficient event clearing
        private IAudioFilter.OnAudioDelegate audioReadEvent;

        // Microphone position tracking for latency optimization
        private int lastMicrophonePosition = 0;
        private int microphoneSampleRate = 48000;
        private int microphoneBufferLengthSeconds = 1;
        private string currentMicrophoneName;
        private bool useMicrophonePositionOptimization = true;

        // Low-latency audio processing
        private AudioClip microphoneClip;
        private float[] realtimeAudioBuffer;
        private float[] processingSamplesBuffer; // Reusable buffer to avoid allocations
        private const int REALTIME_CHUNK_SIZE = 256; // Small chunks for low latency
        private bool useRealtimeProcessing = true;

        // Background thread processing
        private CancellationTokenSource realtimeProcessingCts;
        private bool isRealtimeThreadRunning = false;
        private volatile int currentMicrophonePosition = 0; // Thread-safe position sharing

        // Thread-safe audio data queue
        private readonly Queue<float[]> audioDataQueue = new Queue<float[]>();
        private readonly object queueLock = new object();

        // Logging for debugging LiveKit audio flow
        private int audioSentCounter = 0;

        private void Awake()
        {
            if (voiceChatConfiguration != null)
            {
                audioProcessor = new VoiceChatAudioProcessor(voiceChatConfiguration);
            }
        }

        private void Start()
        {
            // Pre-allocate buffer on macOS to avoid allocations in audio thread
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (tempBuffer == null)
                tempBuffer = new float[8192]; // Pre-allocate large buffer for macOS Core Audio compatibility
#endif
        }

        private void Update()
        {
            // Process real-time microphone input on main thread (AudioClip.GetData requires main thread)
            if (cachedEnabled && useRealtimeProcessing && !string.IsNullOrEmpty(currentMicrophoneName))
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
                return; // No new samples available

            // Calculate how many new samples are available
            int totalBufferSamples = microphoneClip.samples;
            int samplesToRead;

            if (currentMicrophonePosition >= lastMicrophonePosition)
            {
                samplesToRead = currentMicrophonePosition - lastMicrophonePosition;
            }
            else
            {
                // Handle buffer wrap-around (circular buffer)
                samplesToRead = (totalBufferSamples - lastMicrophonePosition) + currentMicrophonePosition;
            }

            // Process in chunks to maintain low latency
            while (samplesToRead > 0)
            {
                int chunkSize = Mathf.Min(samplesToRead, REALTIME_CHUNK_SIZE);

                // Ensure we have buffer allocated for this chunk
                if (realtimeAudioBuffer == null || realtimeAudioBuffer.Length < chunkSize)
                {
                    realtimeAudioBuffer = new float[Mathf.Max(chunkSize, REALTIME_CHUNK_SIZE)];
                }

                // Extract new audio data directly from microphone clip (main thread only)
                microphoneClip.GetData(realtimeAudioBuffer, lastMicrophonePosition);

                // Create a copy for the background thread to avoid shared data issues
                float[] audioChunk = new float[chunkSize];
                Array.Copy(realtimeAudioBuffer, 0, audioChunk, 0, chunkSize);

                // Queue audio data for background processing
                lock (queueLock)
                {
                    audioDataQueue.Enqueue(audioChunk);
                }

                // Update position for next chunk
                lastMicrophonePosition = (lastMicrophonePosition + chunkSize) % totalBufferSamples;
                samplesToRead -= chunkSize;
            }
        }

        private void OnEnable()
        {
            cachedEnabled = true;
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            // Start background real-time processing thread
            StartRealtimeProcessingThread();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"AudioFilter enabled - GameObject: {gameObject.name}, Processing: {cachedEnabled}, Subscribers: {GetSubscriberCount()}");
        }

        private void OnDisable()
        {
            cachedEnabled = false;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;

            StopRealtimeProcessingThread();

            lock (queueLock)
            {
                audioDataQueue.Clear();
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"AudioFilter disabled - GameObject: {gameObject.name}, LiveKit subscribers preserved ({GetSubscriberCount()}), audio queue cleared");
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

            // Real-time processing runs on background thread - this is just fallback
            if (useRealtimeProcessing && useMicrophonePositionOptimization && !string.IsNullOrEmpty(currentMicrophoneName) && isRealtimeThreadRunning)
            {
                // Send silence to prevent Unity from processing this audio since we handle it in Update()
                float[] silenceBuffer = GetSilenceBuffer(data.Length / channels);
                audioReadEvent?.Invoke(silenceBuffer, 1, GetEffectiveSampleRate());
                return;
            }

            // Fallback path: Use Unity's OnAudioFilterRead when real-time processing unavailable
            if (!cachedEnabled)
            {
                float[] silenceBuffer = GetSilenceBuffer(data.Length / channels);
                audioReadEvent?.Invoke(silenceBuffer, 1, GetEffectiveSampleRate());
                return;
            }

            // Always convert to mono first, regardless of processing state
            float[] monoData = ConvertToMono(data, channels);

            if (isProcessingEnabled && audioProcessor != null && voiceChatConfiguration != null && monoData.Length > 0)
            {
                try
                {
                    audioProcessor.ProcessAudio(monoData, GetEffectiveSampleRate());
                    audioReadEvent?.Invoke(monoData, 1, GetEffectiveSampleRate());
                    LogAudioSentToLiveKit("OnAudioFilterRead-Processed", monoData, GetEffectiveSampleRate());
                    return;
                }
                catch (Exception ex)
                {
                    // On macOS, Core Audio is very sensitive to exceptions in audio callbacks
                    // Disable processing to prevent further issues
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error on macOS: {ex.Message}. Disabling processing to prevent audio system instability.");
                    isProcessingEnabled = false;
#else
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Audio processing error: {ex.Message}");
#endif
                }
            }

            // Send mono audio when processing is disabled or failed
            audioReadEvent?.Invoke(monoData, 1, GetEffectiveSampleRate());
            LogAudioSentToLiveKit("OnAudioFilterRead-Fallback", monoData, GetEffectiveSampleRate());
        }

        // Event is called from the Unity audio thread - LiveKit compatibility
        public event IAudioFilter.OnAudioDelegate AudioRead
        {
            add
            {
                audioReadEvent += value;
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"AudioRead subscriber added - Total subscribers: {GetSubscriberCount()}");
            }
            remove
            {
                audioReadEvent -= value;
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"AudioRead subscriber removed - Total subscribers: {GetSubscriberCount()}");
                
                // Log stack trace when subscriber count drops to 0 to help debug unexpected unsubscriptions
                if (GetSubscriberCount() == 0)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"All AudioRead subscribers removed - Stack trace: {System.Environment.StackTrace}");
                }
            }
        }

        public bool IsValid => audioProcessor != null && voiceChatConfiguration != null;

        private void ClearAudioReadSubscribers()
        {
            int subscriberCountBefore = GetSubscriberCount();
            audioReadEvent = null;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Cleared all AudioRead subscribers - Was: {subscriberCountBefore}, Now: 0");
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // Always use Unity's output sample rate as target
            cachedSampleRate = AudioSettings.outputSampleRate;

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // On macOS, log additional audio system information for debugging
            var config = AudioSettings.GetConfiguration();
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"macOS Audio Debug - Unity Config SampleRate: {config.sampleRate}Hz, " +
                $"DSPBufferSize: {config.dspBufferSize}, OutputSampleRate: {AudioSettings.outputSampleRate}Hz");
#endif
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
            cachedEnabled = enabled;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Audio filter processing {(enabled ? "enabled" : "disabled")} - LiveKit subscribers preserved");
        }

        public void SetMicrophoneInfo(string microphoneName, int sampleRate, int bufferLengthSeconds)
        {
            currentMicrophoneName = microphoneName;
            microphoneSampleRate = sampleRate;
            microphoneBufferLengthSeconds = bufferLengthSeconds;
            lastMicrophonePosition = 0;

            // Pre-allocate realtime processing buffers to avoid allocations
            realtimeAudioBuffer = new float[REALTIME_CHUNK_SIZE];
            processingSamplesBuffer = new float[REALTIME_CHUNK_SIZE];

            ReportHub.Log(ReportCategory.VOICE_CHAT, 
                $"AudioFilter microphone info updated - Name: '{microphoneName}', SampleRate: {sampleRate}Hz, Buffer: {bufferLengthSeconds}s");
        }

        public void SetMicrophoneClip(AudioClip clip)
        {
            microphoneClip = clip;
        }

        public void ResetProcessor()
        {
            audioProcessor?.Reset();
            
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Audio processor reset - LiveKit subscribers preserved");
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

            // Start background thread for real-time processing
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
            await UniTask.SwitchToThreadPool(); // Switch to background thread

            try
            {
                const int POLL_INTERVAL_MS = 1; // Poll every 1ms for maximum responsiveness

                while (!ct.IsCancellationRequested && cachedEnabled && useRealtimeProcessing)
                {
                    try
                    {
                        // Process all queued audio chunks
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
                                break; // No more data to process

                            // Process the audio chunk
                            ProcessRealtimeAudioChunk(audioChunk, audioChunk.Length);
                        }

                        // Small delay to prevent excessive CPU usage while maintaining responsiveness
                        await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // Expected cancellation
                    }
                    catch (System.Exception ex)
                    {
                        ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                            $"Real-time audio processing error: {ex.Message}. Continuing...");

                        // Brief pause before retrying to prevent error spam
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
            if (!cachedEnabled || audioProcessor == null || voiceChatConfiguration == null)
                return;

            try
            {
                // Ensure our reusable buffer is large enough (allocation-free after first call)
                if (processingSamplesBuffer == null || processingSamplesBuffer.Length < sampleCount)
                {
                    processingSamplesBuffer = new float[Mathf.Max(sampleCount, REALTIME_CHUNK_SIZE)];
                }

                // Copy only the samples we need to our reusable buffer
                Array.Copy(audioChunk, 0, processingSamplesBuffer, 0, sampleCount);

                // Apply audio processing (noise reduction, filtering, etc.)
                audioProcessor.ProcessAudio(processingSamplesBuffer, microphoneSampleRate, sampleCount);

                // Send processed audio to LiveKit immediately
                audioReadEvent?.Invoke(processingSamplesBuffer, 1, microphoneSampleRate);
                LogAudioSentToLiveKit("RealtimeProcessing", processingSamplesBuffer, microphoneSampleRate, sampleCount);
            }
            catch (Exception ex)
            {
                // On macOS, Core Audio is very sensitive to exceptions
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Real-time audio processing error on macOS: {ex.Message}. Disabling real-time processing.");
                useRealtimeProcessing = false;
#else
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Real-time audio processing error: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// Log audio data being sent to LiveKit for debugging (thread-safe)
        /// </summary>
        private void LogAudioSentToLiveKit(string source, float[] audioData, int sampleRate, int? explicitSampleCount = null)
        {
            if (audioReadEvent == null)
            {
                // No subscribers - audio won't actually be sent
                // Use counter-based throttling instead of time (thread-safe)
                if (audioSentCounter % 120 == 0) // Log every 120 attempts (roughly every 2-3 seconds)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"[{source}] No LiveKit subscribers - audio data not being sent!");
                }
                audioSentCounter++; // Still increment counter even when no subscribers
                return;
            }

            audioSentCounter++;

            // Log every 60 audio chunks (roughly every second) to avoid spam
            if (audioSentCounter % 60 == 0)
            {
                int sampleCount = explicitSampleCount ?? audioData.Length;

                // Calculate simple RMS level to verify there's actual audio
                float rms = 0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    rms += audioData[i] * audioData[i];
                }
                rms = Mathf.Sqrt(rms / sampleCount);

                // Convert RMS to dB for more meaningful audio level
                float dbLevel = rms > 0f ? 20f * Mathf.Log10(rms) : -100f;

                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"[{source}] Sent audio to LiveKit - Samples: {sampleCount}, SampleRate: {sampleRate}Hz, " +
                    $"RMS Level: {rms:F4} ({dbLevel:F1} dB), Subscribers: {GetSubscriberCount()}, Counter: {audioSentCounter}");
            }
        }

        /// <summary>
        /// Get the number of LiveKit audio subscribers for debugging
        /// </summary>
        public int GetSubscriberCount()
        {
            if (audioReadEvent == null) return 0;

            // Use reflection to get invocation list length (safer than direct access)
            try
            {
                var invocationList = audioReadEvent.GetInvocationList();
                return invocationList?.Length ?? 0;
            }
            catch
            {
                return -1; // Unknown
            }
        }

        /// <summary>
        /// Get the effective sample rate to use for audio processing
        /// Prioritizes microphone sample rate over Unity's output sample rate
        /// </summary>
        private int GetEffectiveSampleRate()
        {
            // Use microphone sample rate if available (more accurate for voice processing)
            // Fall back to Unity's output sample rate if microphone info not set
            return microphoneSampleRate > 0 ? microphoneSampleRate : cachedSampleRate;
        }

        private float[] ConvertToMono(float[] data, int channels)
        {
            if (channels == 1)
            {
                // Already mono - return as-is
                return data;
            }

            // Multi-channel input - convert to mono by averaging all channels
            int samplesPerChannel = data.Length / channels;

            // Ensure we have a properly sized buffer
            if (tempBuffer == null || tempBuffer.Length < samplesPerChannel)
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                tempBuffer = new float[Mathf.Max(samplesPerChannel, 8192)];
#else
                tempBuffer = new float[samplesPerChannel];
#endif
            }

            // Average all channels - standard mono conversion
            for (var sampleIndex = 0; sampleIndex < samplesPerChannel; sampleIndex++)
            {
                var sum = 0f;

                for (var ch = 0; ch < channels; ch++) { sum += data[(sampleIndex * channels) + ch]; }

                tempBuffer[sampleIndex] = sum / channels;
            }

            return tempBuffer;
        }

        private float[] GetSilenceBuffer(int length)
        {
            if (silenceBuffer == null || silenceBuffer.Length < length) { silenceBuffer = new float[Mathf.Max(length, 1024)]; }

            for (var i = 0; i < length; i++) { silenceBuffer[i] = 0f; }

            return silenceBuffer;
        }
    }
}
