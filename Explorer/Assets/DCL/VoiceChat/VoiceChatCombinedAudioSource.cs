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
        private const int DEFAULT_LIVEKIT_CHANNELS = 1;

        private readonly HashSet<WeakReference<IAudioStream>> streams = new ();
        private bool isPlaying;
        private int lastDataLength;
        private int sampleRate = 48000;
        private float[] tempBuffer;

        // Feedback detection support
        private readonly float[] speakerOutputBuffer = new float[4096]; // ~85ms at 48kHz
        private int speakerBufferIndex = 0;
        private readonly object speakerBufferLock = new object();

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
                data.AsSpan().Clear();
                return;
            }

            if (tempBuffer == null || tempBuffer.Length != (channels == 2 ? data.Length / 2 : data.Length))
            {
                tempBuffer = new float[channels == 2 ? data.Length / 2 : data.Length];
                lastDataLength = data.Length;
            }

            Span<float> dataSpan = data.AsSpan();
            dataSpan.Clear();
            var activeStreams = 0;

            foreach (WeakReference<IAudioStream> weakStream in streams)
            {
                if (weakStream.TryGetTarget(out IAudioStream stream))
                {
                    Array.Clear(tempBuffer, 0, tempBuffer.Length);

                    // Read mono data
                    stream.ReadAudio(tempBuffer, DEFAULT_LIVEKIT_CHANNELS, sampleRate);

                    if (channels == 2)
                    {
                        // Upmix mono to stereo
                        for (int i = 0, j = 0; i < data.Length; i += 2, j++)
                        {
                            data[i] += tempBuffer[j];     // Left
                            data[i + 1] += tempBuffer[j]; // Right
                        }
                    }
                    else
                    {
                        for (int i = 0; i < data.Length; i++)
                            data[i] += tempBuffer[i];
                    }

                    activeStreams++;
                }
            }

            // Normalize only if multiple streams
            if (activeStreams > 1)
            {
                float norm = 1f / activeStreams;
                for (var i = 0; i < data.Length; i++)
                    data[i] *= norm;
            }

            // Store speaker output for feedback detection
            StoreSpeakerOutput(data, channels);
        }

        /// <summary>
        ///     Stores the current speaker output for feedback detection
        /// </summary>
        private void StoreSpeakerOutput(float[] data, int channels)
        {
            lock (speakerBufferLock)
            {
                int samplesPerChannel = data.Length / channels;
                
                // Convert to mono and store in circular buffer
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    float sample;
                    if (channels == 2)
                    {
                        // Average stereo channels
                        sample = (data[i * 2] + data[i * 2 + 1]) * 0.5f;
                    }
                    else
                    {
                        sample = data[i];
                    }
                    
                    speakerOutputBuffer[speakerBufferIndex] = sample;
                    speakerBufferIndex = (speakerBufferIndex + 1) % speakerOutputBuffer.Length;
                }
            }
        }

        /// <summary>
        ///     Gets the current speaker output buffer for feedback detection
        /// </summary>
        public bool TryGetCurrentSpeakerOutput(float[] outputBuffer, out int sampleCount)
        {
            lock (speakerBufferLock)
            {
                if (outputBuffer.Length >= speakerOutputBuffer.Length)
                {
                    speakerOutputBuffer.CopyTo(outputBuffer, 0);
                    sampleCount = speakerOutputBuffer.Length;
                    return true;
                }
                
                sampleCount = 0;
                return false;
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
            await UniTask.SwitchToMainThread();
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
            await UniTask.SwitchToMainThread();
            audioSource.Stop();
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            sampleRate = AudioSettings.outputSampleRate;
        }
    }
}
