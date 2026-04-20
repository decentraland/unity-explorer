using DCL.Diagnostics;
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
    /// Thread-safe hub for managing LiveKit audio playback sources.
    /// </summary>
    public readonly struct PlaybackSourcesHub
    {
        private readonly AudioMixerGroup audioMixerGroup;
        private readonly bool spatial;

        private readonly Action<StreamKey, LivekitAudioSource>? onSourceConfigured;
        private readonly Action<StreamKey>? onSourceRemoved;

        private readonly Transform parent;

        private readonly ConcurrentDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> streams;
        public IReadOnlyDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> Streams => streams;

        public PlaybackSourcesHub(
            string parentNameSuffix,
            AudioMixerGroup audioMixerGroup,
            bool spatial = false,
            Action<StreamKey, LivekitAudioSource>? onSourceConfigured = null,
            Action<StreamKey>? onSourceRemoved = null)
        {
            this.streams = new ConcurrentDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)>();
            this.audioMixerGroup = audioMixerGroup;
            this.spatial = spatial;
            this.onSourceConfigured = onSourceConfigured;
            this.onSourceRemoved = onSourceRemoved;
            parent = new GameObject("VoiceChatSources_" + parentNameSuffix).transform;
        }

        internal void AddOrReplaceStream(StreamKey key, Weak<AudioStream> stream)
        {
            TryRemoveStream(key);

            LivekitAudioSource source = CreateAndPlaySource(key, stream, audioMixerGroup, parent, spatial);

            if (!streams.TryAdd(key, (stream, source)))
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Cannot add stream key to dictionary, value is already assigned within the key: {key}");
                DisposeSource(source);
                return;
            }

            onSourceConfigured?.Invoke(key, source);
        }

        internal void TryRemoveStream(StreamKey key)
        {
            if (streams.TryRemove(key, out (Weak<AudioStream> stream, LivekitAudioSource source) pair))
            {
                onSourceRemoved?.Invoke(key);
                DisposeSource(pair.source);
            }
        }

        internal void SetMuteAll(bool mute)
        {
            foreach ((Weak<AudioStream> stream, LivekitAudioSource source) pair in streams.Values)
                pair.source.AudioSource.mute = mute;
        }

        internal void Stop()
        {
            foreach ((Weak<AudioStream> stream, LivekitAudioSource source) pair in streams.Values)
                pair.source.Stop();
        }

        internal void Reset()
        {
            foreach (StreamKey key in streams.Keys)
                TryRemoveStream(key);
        }

        internal void SetMuteForIdentity(string identity, bool mute)
        {
            foreach (KeyValuePair<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> kvp in streams)
                if (kvp.Key.identity == identity)
                {
                    kvp.Value.source.AudioSource.mute = mute;
                    return;
                }
        }

        internal void RemoveStreamsByIdentity(string identity)
        {
            foreach (StreamKey key in streams.Keys)
                if (key.identity == identity)
                {
                    TryRemoveStream(key);
                    return;
                }
        }

        private static LivekitAudioSource CreateAndPlaySource(StreamKey key, Weak<AudioStream> stream, AudioMixerGroup mixerGroup, Transform parent, bool spatial = false)
        {
            LivekitAudioSource lkSource = LivekitAudioSource.New(true, spatial);
            lkSource.Construct(stream);
            lkSource.AudioSource.EnsureNotNull().outputAudioMixerGroup = mixerGroup;
            lkSource.name = $"LivekitSource_{key.identity}";
            lkSource.transform.SetParent(parent);
            lkSource.Play();

            return lkSource;
        }

        private static void DisposeSource(LivekitAudioSource livekitAudioSource)
        {
            livekitAudioSource.Stop();
            livekitAudioSource.Free();
            livekitAudioSource.gameObject.SelfDestroy();
        }
    }
}
