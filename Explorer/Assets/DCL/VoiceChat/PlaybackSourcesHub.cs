using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> streams;
        private readonly AudioMixerGroup audioMixerGroup;

        private readonly Transform parent;

        public IReadOnlyDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> Streams =>
            streams;

        internal PlaybackSourcesHub(AudioMixerGroup audioMixerGroup)
        {
            this.streams = new ();
            this.audioMixerGroup = audioMixerGroup;
            parent = new GameObject(nameof(PlaybackSourcesHub)).transform;
        }

        internal void AddOrReplaceStream(StreamKey key, Weak<AudioStream> stream)
        {
            if (streams.TryRemove(key, out var oldStream))
                DisposeSource(oldStream.source);

            LivekitAudioSource source = LivekitAudioSource.New(true);
            source.Construct(stream);
            AudioSource audioSource = source.GetComponent<AudioSource>().EnsureNotNull();
            audioSource.outputAudioMixerGroup = audioMixerGroup;
            source.name = $"LivekitSource_{key.identity}";
            source.transform.SetParent(parent);

            if (streams.TryAdd(key, (stream, source)) == false)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Cannot add stream key to dictionary, value is already assigned within the key: {key}");
                DisposeSource(source);
            }
        }

        internal void RemoveStream(StreamKey key)
        {
            if (streams.TryRemove(key, out (Weak<AudioStream> stream, LivekitAudioSource source) pair))
                InternalAsync(pair.source).Forget();

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
                foreach ((Weak<AudioStream> stream, LivekitAudioSource source) pair in hub.streams.Values)
                    pair.source.Play();
            });
        }

        internal void Stop()
        {
            ExecuteOnMainThread(this, static hub =>
            {
                foreach ((Weak<AudioStream> stream, LivekitAudioSource source) pair in hub.streams.Values)
                    pair.source.Stop();
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
