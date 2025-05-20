using System;
using System.Collections.Generic;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;

namespace DCL.VoiceChat
{
    public class VoiceChatCombinedAudioSource : MonoBehaviour
    {
        private static ulong counter;
        [field: SerializeField] private AudioSource audioSource;
        [SerializeField] private bool enableAudioMonitoring = true;
        [SerializeField] private float audioLevelThreshold = 0.01f;
        private readonly HashSet<WeakReference<IAudioStream>> streams = new ();
        private float currentAudioLevel;
        private bool isPlaying;
        private float peakAudioLevel;
        private int sampleRate;
        private float[] tempBuffer;

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
            if (!isPlaying || streams.Count == 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            if (tempBuffer == null || tempBuffer.Length != data.Length) { tempBuffer = new float[data.Length]; }

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

            if (activeStreams > 1)
            {
                float norm = 1f / activeStreams;

                for (var i = 0; i < data.Length; i++)
                    data[i] *= norm;
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

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            sampleRate = AudioSettings.outputSampleRate;
        }
    }
}
