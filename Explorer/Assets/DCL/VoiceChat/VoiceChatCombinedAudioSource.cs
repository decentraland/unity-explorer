using System;
using System.Collections.Generic;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;
using Utility.Multithreading;
using Cysharp.Threading.Tasks;

namespace DCL.VoiceChat
{
    public class VoiceChatCombinedAudioSource : MonoBehaviour
    {
        [field: SerializeField] private AudioSource audioSource;
        private readonly HashSet<WeakReference<IAudioStream>> streams = new ();
        private bool isPlaying;
        private int sampleRate = 48000;
        private float[] tempBuffer;
        private int lastDataLength = 0;

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isPlaying || streams.Count == 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            if (tempBuffer == null || lastDataLength != data.Length)
            {
                tempBuffer = new float[data.Length];
                lastDataLength = data.Length;
            }

            Array.Clear(data, 0, data.Length);
            var activeStreams = 0;

            foreach (WeakReference<IAudioStream> weakStream in streams)
            {
                if (weakStream.TryGetTarget(out IAudioStream stream))
                {
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);
                    stream.ReadAudio(tempBuffer, channels, sampleRate);

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

            // Copy left channel to right channel for proper stereo output
            // LiveKit audio is mono, so duplicate left channel to avoid single-ear audio
            if (channels == 2)
            {
                for (var i = 0; i < data.Length; i += 2)
                    data[i + 1] = data[i];
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
