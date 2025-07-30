using System;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatCombinedStreamsAudioSource : MonoBehaviour
    {
        [field: SerializeField] private AudioSource audioSource;
        [field: SerializeField] private VoiceChatCombinedStreamsAudioFilter audioFilter;
        private readonly Dictionary<IAudioStream, LivekitAudioSource> sourcesMap = new();

        private bool isPlaying;
        private int sampleRate = 48000;

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            audioFilter.Clear();
            isPlaying = false;
            sampleRate = 48000;
        }

        private void OnDestroy()
        {
            audioFilter.Dispose();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            audioFilter.ProcessAudioForLiveKit(data, channels, sampleRate);
        }

        public void AddStream(WeakReference<IAudioStream> weakStream)
        {
            /*if (weakStream.TryGetTarget(out var audioStream))
            {
                if (sourcesMap.ContainsKey(audioStream) == false)
                {
                    var livekitAudioSource = LivekitAudioSource.New(true);
                    livekitAudioSource.Construct(weakStream);
                    livekitAudioSource.Play();
                    sourcesMap[audioStream] = livekitAudioSource;
                }
            }*/
            audioFilter.AddStream(weakStream);
        }

        public void RemoveStream(WeakReference<IAudioStream> stream)
        {
            if (stream.TryGetTarget(out var audioStream))
            {
                if (sourcesMap.TryGetValue(audioStream, out var audioSource))
                {
                    audioSource.Stop();
                    audioSource.SelfDestroy();
                    sourcesMap.Remove(audioStream);
                }
            }

            audioFilter.RemoveStream(stream);
        }

        public void Reset()
        {
            audioFilter.Reset();

            foreach (var audioSource in sourcesMap.Values)
            {
                audioSource.SelfDestroy();
            }

            sourcesMap.Clear();
            isPlaying = false;
        }

        public void Play()
        {
            isPlaying = true;

            if (!PlayerLoopHelper.IsMainThread)
            {
                PlayAsync().Forget();
                return;
            }

            PlayInternal();
            return;

            void PlayInternal()
            {
                foreach (var livekitAudioSource in sourcesMap.Values)
                {
                    livekitAudioSource.Play();
                }

                audioSource.Play();
            }

            async UniTaskVoid PlayAsync()
            {
                await UniTask.SwitchToMainThread();
                PlayInternal();
            }

        }


        public void Stop()
        {
            isPlaying = false;

            if (!PlayerLoopHelper.IsMainThread)
            {
                StopAsync().Forget();
                return;
            }

            StopInternal();
            return;

            void StopInternal()
            {
                foreach (var livekitAudioSource in sourcesMap.Values)
                {
                    livekitAudioSource.Stop();
                }

                audioSource.Stop();
            }

            async UniTaskVoid StopAsync()
            {
                await UniTask.SwitchToMainThread();
                StopInternal();
            }

        }


        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            sampleRate = AudioSettings.outputSampleRate;
            audioFilter.SetSampleRate(sampleRate);
        }
    }
}
