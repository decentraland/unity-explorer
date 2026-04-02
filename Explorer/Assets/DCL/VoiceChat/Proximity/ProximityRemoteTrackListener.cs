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
        private readonly IRoom islandRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ConcurrentDictionary<StreamKey, LivekitAudioSource> remoteSources = new ();
        private readonly Transform fallbackParent;

        private LivekitAudioSource? loopbackSource;
        private bool suppressed;

        internal bool IsSuppressed => suppressed;

        internal ProximityRemoteTrackListener(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            ConcurrentDictionary<string, AudioSource> activeAudioSources)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.activeAudioSources = activeAudioSources;

            fallbackParent = new GameObject($"{nameof(ProximityRemoteTrackListener)}_FallbackParent").transform;
        }

        public void Dispose()
        {
            StopListening();

            if (fallbackParent != null)
                fallbackParent.gameObject.SelfDestroy();
        }

        internal void StartListening()
        {
            foreach (KeyValuePair<string, Participant> entry in islandRoom.Participants.RemoteParticipantIdentities())
            foreach ((string sid, TrackPublication pub) in entry.Value.Tracks)
            {
                if (pub.Kind != TrackKind.KindAudio) continue;

                var key = new StreamKey(entry.Key!, sid);
                Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);

                if (stream.Resource.Has)
                    AddRemoteSource(key, stream);
            }
        }

        internal void StopListening()
        {
            DestroySource(loopbackSource);
            loopbackSource = null;

            foreach (StreamKey key in remoteSources.Keys)
                RemoveRemoteSource(key);
        }

        internal void HandleTrackSubscribed(TrackPublication publication, Participant participant)
        {
            HandleTrackSubscribedAsync().Forget();
            return;

            async UniTaskVoid HandleTrackSubscribedAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;

                try
                {
                    var key = new StreamKey(participant.Identity, publication.Sid);
                    Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);

                    if (stream.Resource.Has)
                        AddRemoteSource(key, stream);
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle track subscription: {ex.Message}");
                }
            }
        }

        internal void HandleTrackUnsubscribed(TrackPublication publication, Participant participant)
        {
            HandleTrackUnsubscribedAsync().Forget();
            return;

            async UniTaskVoid HandleTrackUnsubscribedAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;

                try
                {
                    RemoveRemoteSource(new StreamKey(participant.Identity, publication.Sid));
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle track unsubscription: {ex.Message}");
                }
            }
        }

        internal void HandleLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            HandleLocalTrackPublishedAsync().Forget();
            return;

            async UniTaskVoid HandleLocalTrackPublishedAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;
                if (!configuration.EnableLocalTrackPlayback) return;

                try
                {
                    var key = new StreamKey(participant.Identity, publication.Sid);
                    Weak<AudioStream> stream = islandRoom.AudioStreams.ActiveStream(key);

                    if (stream.Resource.Has)
                    {
                        loopbackSource = CreateSource(key, stream, spatial: false);
                        loopbackSource.transform.SetParent(fallbackParent);
                        loopbackSource.Play();
                        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Local track loopback enabled (round-trip via server)");
                    }
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle local track published: {ex.Message}");
                }
            }
        }

        internal void HandleLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            HandleLocalTrackUnpublishedAsync().Forget();
            return;

            async UniTaskVoid HandleLocalTrackUnpublishedAsync()
            {
                await UniTask.SwitchToMainThread();

                if (publication.Kind != TrackKind.KindAudio) return;
                if (!configuration.EnableLocalTrackPlayback) return;

                try
                {
                    DestroySource(loopbackSource);
                    loopbackSource = null;
                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "Local track loopback removed");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"Failed to handle local track unpublished: {ex.Message}");
                }
            }
        }

        internal void MuteAll()
        {
            if (suppressed) return;
            suppressed = true;

            foreach (LivekitAudioSource source in remoteSources.Values)
            {
                AudioSource audioSource = source.GetComponent<AudioSource>();
                if (audioSource != null) audioSource.mute = true;
            }
        }

        internal void UnmuteAll()
        {
            if (!suppressed) return;
            suppressed = false;

            foreach (KeyValuePair<StreamKey, LivekitAudioSource> entry in remoteSources)
            {
                AudioSource audioSource = entry.Value.GetComponent<AudioSource>();
                if (audioSource != null) audioSource.mute = false;
            }
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
                ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, $"Cannot add proximity source, key already exists: {key}");
                DestroySource(source);
                return;
            }

            AudioSource audioSource = source.GetComponent<AudioSource>();
            activeAudioSources[key.identity] = audioSource;

            if (audioSource != null && suppressed)
                audioSource.mute = true;

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"3D audio source added for {key.identity}{(suppressed ? " (muted — call active)" : "")}");
        }

        private void RemoveRemoteSource(StreamKey key)
        {
            if (!remoteSources.TryRemove(key, out LivekitAudioSource? source))
                return;

            activeAudioSources.TryRemove(key.identity, out _);
            DestroySource(source);
            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"Remote source removed for {key.identity}");
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
