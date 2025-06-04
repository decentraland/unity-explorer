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

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        // Called by Unity on the Audio thread
        private void OnAudioFilterRead(float[] data, int channels)
        {
            audioFrameCounter++;
            
            if (!isPlaying || streams.Count == 0)
            {
                // Log every 300 frames when no audio (roughly every 6-7 seconds at 48kHz)
                if (audioFrameCounter % 300 == 0)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, 
                        $"[CombinedAudioSource] No audio streams - isPlaying: {isPlaying}, streams.Count: {streams.Count}, frame: {audioFrameCounter}");
                }
                Array.Clear(data, 0, data.Length);
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

            foreach (WeakReference<IAudioStream> weakStream in streams)
            {
                if (weakStream.TryGetTarget(out IAudioStream stream))
                {
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);
                    stream.ReadAudio(tempBuffer, channels, sampleRate);

                    // Calculate RMS for this stream (for logging)
                    if (audioFrameCounter % 180 == 0) // Log every 180 frames (roughly every 3-4 seconds)
                    {
                        float streamRms = 0f;
                        for (var i = 0; i < tempBuffer.Length; i++)
                            streamRms += tempBuffer[i] * tempBuffer[i];
                        streamRms = Mathf.Sqrt(streamRms / tempBuffer.Length);
                        totalRms += streamRms;
                    }

                    for (var i = 0; i < tempBuffer.Length; i++)
                        data[i] += tempBuffer[i];

                    activeStreams++;
                }
            }

            if (activeStreams > 1)
            {
                float norm = 1f / activeStreams;

                for (var i = 0; i < data.Length; i++)
                    data[i] *= norm;
            }

            // Throttled logging every 180 frames (roughly every 3-4 seconds)
            if (audioFrameCounter % 180 == 0 && activeStreams > 0)
            {
                float dbLevel = totalRms > 0f ? 20f * Mathf.Log10(totalRms) : -100f;
                ReportHub.Log(ReportCategory.VOICE_CHAT, 
                    $"[CombinedAudioSource] Receiving audio - ActiveStreams: {activeStreams}, " +
                    $"TotalStreams: {streams.Count}, RMS: {totalRms:F4} ({dbLevel:F1} dB), " +
                    $"SampleRate: {sampleRate}Hz, Channels: {channels}, Frame: {audioFrameCounter}");
            }
        }

        public void AddStream(WeakReference<IAudioStream> stream)
        {
            streams.Add(stream);
        }

        public void RemoveStream(WeakReference<IAudioStream> stream)
        {
            streams.Remove(stream);
        }

        public void Free()
        {
            streams.Clear();
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
        }
        
        private async UniTaskVoid PlayAsync()
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();
            audioSource.Play();
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
        }
        
        private async UniTaskVoid StopAsync()
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();
            audioSource.Stop();
        }

        public void SetVolume(float target)
        {
            audioSource.volume = target;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            sampleRate = AudioSettings.outputSampleRate;
        }
    }
}
