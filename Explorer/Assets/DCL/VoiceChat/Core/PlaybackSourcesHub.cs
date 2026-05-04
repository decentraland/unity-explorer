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
        // Read as ConcurrentDictionary<string, ConcurrentHashSet<StreamKey>> — byte is an unused placeholder (no ConcurrentHashSet in BCL).
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<StreamKey, byte>> streamKeysByIdentity;
        public IReadOnlyDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> Streams => streams;

        public PlaybackSourcesHub(
            string parentNameSuffix,
            AudioMixerGroup audioMixerGroup,
            bool spatial = false,
            Action<StreamKey, LivekitAudioSource>? onSourceConfigured = null,
            Action<StreamKey>? onSourceRemoved = null)
        {
            this.streams = new ConcurrentDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)>();
            this.streamKeysByIdentity = new ConcurrentDictionary<string, ConcurrentDictionary<StreamKey, byte>>();
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

            streamKeysByIdentity
               .GetOrAdd(key.identity, static _ => new ConcurrentDictionary<StreamKey, byte>())
               .TryAdd(key, 0); // 0 is a dummy value — inner dict is used as a set, only keys matter.

            onSourceConfigured?.Invoke(key, source);
        }

        internal void TryRemoveStream(StreamKey key)
        {
            if (streams.TryRemove(key, out (Weak<AudioStream> stream, LivekitAudioSource source) pair))
            {
                if (streamKeysByIdentity.TryGetValue(key.identity, out ConcurrentDictionary<StreamKey, byte>? keys))
                {
                    keys.TryRemove(key, out _);
                    if (keys.IsEmpty)
                        streamKeysByIdentity.TryRemove(key.identity, out _);
                }

                onSourceRemoved?.Invoke(key);
                DisposeSource(pair.source);
            }
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
            if (!streamKeysByIdentity.TryGetValue(identity, out ConcurrentDictionary<StreamKey, byte>? keys))
                return;

            foreach (KeyValuePair<StreamKey, byte> kvp in keys)
                if (streams.TryGetValue(kvp.Key, out (Weak<AudioStream> stream, LivekitAudioSource source) pair))
                    pair.source.AudioSource.mute = mute;
        }

        internal void RemoveStreamsByIdentity(string identity)
        {
            if (!streamKeysByIdentity.TryGetValue(identity, out ConcurrentDictionary<StreamKey, byte>? keys))
                return;

            foreach (KeyValuePair<StreamKey, byte> kvp in keys)
                TryRemoveStream(kvp.Key);
        }

        private static LivekitAudioSource CreateAndPlaySource(StreamKey key, Weak<AudioStream> stream, AudioMixerGroup mixerGroup, Transform parent, bool spatial = false)
        {
            LivekitAudioSource lkSource = LivekitAudioSource.New(true, spatial);
            lkSource.Construct(stream);
            lkSource.AudioSource.EnsureNotNull().outputAudioMixerGroup = mixerGroup;
            lkSource.name = $"LivekitSource_{key.identity}";
            lkSource.transform.SetParent(parent);

            // fix for: start spatial sources muted until NearbyAudioPositionSystem syncs position — prevents audio burst at world origin.
            lkSource.AudioSource.mute = spatial;
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
