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

        private readonly Dictionary<IAudioStream, LivekitAudioSource> sourcesMap = new ();

        private int sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;

        public void Reset()
        {
            audioFilter.Reset();

            foreach (LivekitAudioSource audioSource in sourcesMap.Values) { audioSource.SelfDestroy(); }

            sourcesMap.Clear();
        }

        private void OnEnable()
        {
            OnAudioConfigurationChanged(false);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            audioFilter.Clear();
            sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
        }

        private void OnDestroy()
        {
            audioFilter.Dispose();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            audioFilter.ProcessAudioForLiveKit(data, sampleRate);
        }

        public void AddStream(WeakReference<IAudioStream> weakStream)
        {
            audioFilter.AddStream(weakStream);
        }

        public void RemoveStream(WeakReference<IAudioStream> stream)
        {
            if (stream.TryGetTarget(out IAudioStream audioStream))
            {
                if (sourcesMap.TryGetValue(audioStream, out LivekitAudioSource audioSource))
                {
                    audioSource.Stop();
                    audioSource.SelfDestroy();
                    sourcesMap.Remove(audioStream);
                }
            }

            audioFilter.RemoveStream(stream);
        }

        public void Play()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                PlayAsync().Forget();
                return;
            }

            PlayInternal();
            return;

            void PlayInternal()
            {
                foreach (LivekitAudioSource livekitAudioSource in sourcesMap.Values) { livekitAudioSource.Play(); }

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
            if (!PlayerLoopHelper.IsMainThread)
            {
                StopAsync().Forget();
                return;
            }

            StopInternal();
            return;

            void StopInternal()
            {
                foreach (LivekitAudioSource livekitAudioSource in sourcesMap.Values) { livekitAudioSource.Stop(); }

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
