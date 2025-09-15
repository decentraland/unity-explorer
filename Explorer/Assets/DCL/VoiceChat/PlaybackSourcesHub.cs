using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using LiveKit.Rooms.Streaming.Audio;
using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Thread-safe
    /// </summary>
    internal readonly struct PlaybackSourcesHub
    {
        private readonly ConcurrentDictionary<StreamKey, LivekitAudioSource> streams;
        private readonly AudioMixerGroup audioMixerGroup;

        public PlaybackSourcesHub(ConcurrentDictionary<StreamKey, LivekitAudioSource> streams, AudioMixerGroup audioMixerGroup)
        {
            this.streams = streams;
            this.audioMixerGroup = audioMixerGroup;
        }

        public void AddStream(StreamKey key, WeakReference<IAudioStream> stream)
        {
            LivekitAudioSource source = LivekitAudioSource.New(true);
            source.Construct(stream);
            AudioSource audioSource = source.GetComponent<AudioSource>().EnsureNotNull();
            audioSource.outputAudioMixerGroup = audioMixerGroup;

            if (streams.TryAdd(key, source) == false)
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Cannot add stream key to dictionary, value is already assigned within the key: {key}");
        }

        public void RemoveStream(StreamKey key)
        {
            if (streams.TryRemove(key, out LivekitAudioSource source))
                InternalAsync(source!).Forget();

            return;

            static async UniTaskVoid InternalAsync(LivekitAudioSource livekitAudioSource)
            {
                await UniTask.SwitchToMainThread();
                livekitAudioSource.Stop();
                livekitAudioSource.Free();
                livekitAudioSource.gameObject.SelfDestroy();
            }
        }

        public void Reset()
        {
            using var _ = ThreadSafeListPool<StreamKey>.SHARED.Get(out var list);
            foreach (StreamKey streamsKey in streams.Keys) list.Add(streamsKey);

            foreach (StreamKey streamKey in list) RemoveStream(streamKey);
        }

        public void Play()
        {
            ConcurrentDictionary<StreamKey, LivekitAudioSource> streamsRef = streams;

            if (!PlayerLoopHelper.IsMainThread)
            {
                PlayAsync().Forget();
                return;
            }

            PlayInternal();
            return;

            void PlayInternal()
            {
                foreach (LivekitAudioSource livekitAudioSource in streamsRef.Values)
                    livekitAudioSource.Play();
            }

            async UniTaskVoid PlayAsync()
            {
                await UniTask.SwitchToMainThread();
                PlayInternal();
            }
        }

        public void Stop()
        {
            ConcurrentDictionary<StreamKey, LivekitAudioSource> streamsRef = streams;

            if (!PlayerLoopHelper.IsMainThread)
            {
                StopAsync().Forget();
                return;
            }

            StopInternal();
            return;

            void StopInternal()
            {
                foreach (LivekitAudioSource livekitAudioSource in streamsRef.Values)
                    livekitAudioSource.Stop();
            }

            async UniTaskVoid StopAsync()
            {
                await UniTask.SwitchToMainThread();
                StopInternal();
            }
        }
    }
}
