using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Manages the lifecycle of remote audio sources for proximity voice chat:
    /// subscribing to remote tracks, creating spatial 3D GameObjects, registering
    /// them in the shared dictionary for ECS position sync, and mute/unmute.
    /// </summary>
    internal class ProximityRemoteTrackListener : IDisposable
    {
        private const string TAG = nameof(ProximityRemoteTrackListener);

        private readonly IRoom islandRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ConcurrentDictionary<StreamKey, LivekitAudioSource> remoteSources = new ();
        private readonly Transform fallbackParent;

        private LivekitAudioSource? loopbackSource;

        internal bool IsSuppressed { get; private set; }

        internal ProximityRemoteTrackListener(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            ConcurrentDictionary<string, AudioSource> activeAudioSources)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.activeAudioSources = activeAudioSources;

            fallbackParent = new GameObject($"{TAG}_FallbackParent").transform;
        }

        public void Dispose()
        {
            StopListening();

            if (fallbackParent != null)
                fallbackParent.gameObject.SelfDestroy();
        }

        internal void StartListening()
        {
            try
            {
                foreach (KeyValuePair<string, Participant> entry in islandRoom.Participants.RemoteParticipantIdentities())
                foreach ((string sid, TrackPublication pub) in entry.Value.Tracks)
                    if (TryAddRemoteSource(pub.Kind, new StreamKey(entry.Key!, sid)))
                        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Added existing remote track from {entry.Key}");

                ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Remote track listening started");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Failed to start listening: {ex.Message}");
                throw;
            }
        }

        internal void StopListening()
        {
            DestroySource(loopbackSource);
            loopbackSource = null;

            foreach (StreamKey key in remoteSources.Keys)
                RemoveRemoteSource(key);
        }

        internal void HandleTrackSubscribed(TrackPublication publication, Participant participant, bool isLocalLoopback = false)
        {
            HandleAsync().Forget();
            return;

            async UniTaskVoid HandleAsync()
            {
                await UniTask.SwitchToMainThread();

                try
                {
                    if (isLocalLoopback && !configuration.EnableLocalTrackPlayback) return;

                    var key = new StreamKey(participant.Identity, publication.Sid);

                    if (isLocalLoopback)
                    {
                        if (TryAddLoopbackSource(publication.Kind, key))
                            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track loopback enabled (round-trip via server)");
                    }
                    else
                    {
                        if (TryAddRemoteSource(publication.Kind, key))
                            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Track subscribed from {participant.Identity}");
                    }
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT,
                        $"{TAG} Failed to handle track subscription: {ex.Message}{(isLocalLoopback ? " (loopback)" : "")}");
                }
            }
        }

        internal void HandleTrackUnsubscribed(TrackPublication publication, Participant participant, bool isLocalLoopback = false)
        {
            HandleAsync().Forget();
            return;

            async UniTaskVoid HandleAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;

                try
                {
                    if (isLocalLoopback)
                    {
                        if (!configuration.EnableLocalTrackPlayback) return;

                        DestroySource(loopbackSource);
                        loopbackSource = null;
                        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track loopback removed");
                    }
                    else
                    {
                        RemoveRemoteSource(new StreamKey(participant.Identity, publication.Sid));
                    }
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT,
                        $"{TAG} Failed to handle track unsubscription: {ex.Message}{(isLocalLoopback ? " (loopback)" : "")}");
                }
            }
        }

        internal void MuteAll()
        {
            if (IsSuppressed) return;
            IsSuppressed = true;

            foreach (LivekitAudioSource source in remoteSources.Values)
            {
                AudioSource audioSource = source.GetComponent<AudioSource>();
                if (audioSource != null) audioSource.mute = true;
            }
        }

        internal void UnmuteAll()
        {
            if (!IsSuppressed) return;
            IsSuppressed = false;

            foreach (KeyValuePair<StreamKey, LivekitAudioSource> entry in remoteSources)
            {
                AudioSource audioSource = entry.Value.GetComponent<AudioSource>();
                if (audioSource != null) audioSource.mute = false;
            }
        }

        private bool TryAddRemoteSource(TrackKind kind, StreamKey key)
        {
            if (kind != TrackKind.KindAudio) return false;

            Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);
            if (!stream.Resource.Has) return false;

            AddRemoteSource(key, stream);
            return true;
        }

        private bool TryAddLoopbackSource(TrackKind kind, StreamKey key)
        {
            if (kind != TrackKind.KindAudio) return false;

            Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);
            if (!stream.Resource.Has) return false;

            loopbackSource = CreateSource(key, stream, spatial: false);
            loopbackSource.transform.SetParent(fallbackParent);
            loopbackSource.Play();
            return true;
        }

        private void AddRemoteSource(StreamKey key, Weak<AudioStream> stream)
        {
            if (remoteSources.TryRemove(key, out LivekitAudioSource? oldSource))
                DestroySource(oldSource);

            LivekitAudioSource source = CreateSource(key, stream, spatial: true);
            source.transform.SetParent(fallbackParent);
            source.Play();

            if (!remoteSources.TryAdd(key, source))
            {
                ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Cannot add source, key already exists: {key}");
                DestroySource(source);
                return;
            }

            AudioSource audioSource = source.GetComponent<AudioSource>();
            activeAudioSources[key.identity] = audioSource;

            if (audioSource != null && IsSuppressed)
                audioSource.mute = true;
        }

        private void RemoveRemoteSource(StreamKey key)
        {
            if (!remoteSources.TryRemove(key, out LivekitAudioSource? source))
                return;

            activeAudioSources.TryRemove(key.identity, out _);
            DestroySource(source);
        }

        private LivekitAudioSource CreateSource(StreamKey key, Weak<AudioStream> stream, bool spatial)
        {
            LivekitAudioSource source = LivekitAudioSource.New(explicitName: true, spatial: spatial);

            AudioSource audioSource = source.GetComponent<AudioSource>().EnsureNotNull();
            audioSource.outputAudioMixerGroup = configuration.ProximityChatAudioMixerGroup;

            if (spatial)
            {
                configuration.ApplyProximitySettingsTo(audioSource);
                configuration.ApplySpatializationSettingsTo(source);
                source.gameObject.AddComponent<ProximityPanCalculator>();
            }

            source.Construct(stream);
            source.name = $"ProximityAudio_{key.identity}";
            return source;
        }

        private static void DestroySource(LivekitAudioSource? source)
        {
            if (source == null) return;

            source.Stop();
            source.Free();
            source.gameObject.SelfDestroy();
        }
    }
}
