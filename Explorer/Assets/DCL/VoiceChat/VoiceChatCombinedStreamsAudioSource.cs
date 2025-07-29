using System;
using UnityEngine;
using LiveKit.Rooms.Streaming.Audio;
using Cysharp.Threading.Tasks;

namespace DCL.VoiceChat
{
    public class VoiceChatCombinedStreamsAudioSource : MonoBehaviour
    {
        [field: SerializeField] private AudioSource audioSource;
        [field: SerializeField] private VoiceChatCombinedStreamsAudioFilter audioFilter;
        private bool isPlaying;
        private int sampleRate = 48000;

        public VoiceChatCombinedStreamsAudioFilter AudioFilter => audioFilter;

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
            audioFilter.AddStream(weakStream);
        }

        public void RemoveStream(WeakReference<IAudioStream> stream)
        {
            audioFilter.RemoveStream(stream);
        }

        public void Reset()
        {
            audioFilter.Reset();
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
            audioFilter.SetSampleRate(sampleRate);
        }
    }
}
