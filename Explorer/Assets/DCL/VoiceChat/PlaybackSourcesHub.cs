using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using LiveKit.Rooms.Streaming;
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
    public readonly struct PlaybackSourcesHub
    {
        private readonly ConcurrentDictionary<StreamKey, LivekitAudioSource> streams;
        private readonly AudioMixerGroup audioMixerGroup;

        private readonly Transform parent;

        internal PlaybackSourcesHub(ConcurrentDictionary<StreamKey, LivekitAudioSource> streams, AudioMixerGroup audioMixerGroup)
        {
            this.streams = streams;
            this.audioMixerGroup = audioMixerGroup;
            parent = new GameObject(nameof(PlaybackSourcesHub)).transform;
        }

        internal void AddOrReplaceStream(StreamKey key, WeakReference<AudioStream> stream)
        {
            if (streams.TryRemove(key, out var oldStream))
                DisposeSource(oldStream!);

            LivekitAudioSource source = LivekitAudioSource.New(true);
            source.Construct(stream);
            AudioSource audioSource = source.GetComponent<AudioSource>().EnsureNotNull();
            audioSource.outputAudioMixerGroup = audioMixerGroup;
            source.name = $"LivekitSource_{key.identity}";
            source.transform.SetParent(parent);

            if (streams.TryAdd(key, source) == false)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Cannot add stream key to dictionary, value is already assigned within the key: {key}");
                DisposeSource(source);
            }
        }

        internal void RemoveStream(StreamKey key)
        {
            if (streams.TryRemove(key, out LivekitAudioSource source))
                InternalAsync(source!).Forget();

            return;

            static async UniTaskVoid InternalAsync(LivekitAudioSource livekitAudioSource)
            {
                await UniTask.SwitchToMainThread();
                DisposeSource(livekitAudioSource);
            }
        }

        internal void Reset()
        {
            using var _ = ThreadSafeListPool<StreamKey>.SHARED.Get(out var list);
            foreach (StreamKey streamsKey in streams.Keys) list.Add(streamsKey);

            foreach (StreamKey streamKey in list) RemoveStream(streamKey);
        }

        internal void Play()
        {
            ExecuteOnMainThread(this, static hub =>
            {
                foreach (LivekitAudioSource livekitAudioSource in hub.streams.Values)
                    livekitAudioSource.Play();
            });
        }

        internal void Stop()
        {
            ExecuteOnMainThread(this, static hub =>
            {
                foreach (LivekitAudioSource livekitAudioSource in hub.streams.Values)
                    livekitAudioSource.Stop();
            });
        }

        private static void DisposeSource(LivekitAudioSource livekitAudioSource)
        {
            livekitAudioSource.Stop();
            livekitAudioSource.Free();
            livekitAudioSource.gameObject.SelfDestroy();
        }

        private static void ExecuteOnMainThread(PlaybackSourcesHub sourcesHub, Action<PlaybackSourcesHub> action)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                ExecuteAsync().Forget();
                return;
            }

            action(sourcesHub);
            return;

            async UniTaskVoid ExecuteAsync()
            {
                await UniTask.SwitchToMainThread();
                action(sourcesHub);
            }
        }
    }
}
