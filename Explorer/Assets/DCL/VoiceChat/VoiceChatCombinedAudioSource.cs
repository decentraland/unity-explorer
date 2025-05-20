using System;
using System.Collections.Generic;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;

namespace DCL.VoiceChat
{
    public class VoiceChatCombinedAudioSource : MonoBehaviour
    {
        [field:SerializeField] private AudioSource audioSource;

        private static ulong counter;
        private readonly HashSet<WeakReference<IAudioStream>> streams = new();
        private int sampleRate;
        private bool isPlaying;

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
            audioSource.Play();
        }

        public void Stop()
        {
            isPlaying = false;
            audioSource.Stop();
        }

        public void SetVolume(float target)
        {
            audioSource.volume = target;
        }

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        // Called by Unity on the Audio thread
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isPlaying || streams.Count == 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            Array.Clear(data, 0, data.Length);
            float[] temp = new float[data.Length];
            int activeStreams = 0;

            foreach (var weakStream in streams)
            {
                if (weakStream.TryGetTarget(out var stream) && stream != null)
                {
                    Array.Clear(temp, 0, temp.Length);
                    stream.ReadAudio(temp, channels, sampleRate);
                    for (int i = 0; i < data.Length; i++)
                        data[i] += temp[i];
                    activeStreams++;
                }
            }

            if (activeStreams > 1)
            {
                float norm = 1f / activeStreams;
                for (int i = 0; i < data.Length; i++)
                    data[i] *= norm;
            }
        }
    }
}
