using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
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

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ConcurrentDictionary<StreamKey, LivekitAudioSource> remoteSources = new ();
        private readonly Transform fallbackParent;

        internal bool IsSuppressed { get; private set; }

        internal ProximityRemoteTrackListener(IRoom voiceChatRoom, VoiceChatConfiguration configuration, ConcurrentDictionary<string, AudioSource> activeAudioSources)
        {
            this.voiceChatRoom = voiceChatRoom;
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

        internal async UniTaskVoid StartListening()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                foreach (KeyValuePair<string, Participant> entry in voiceChatRoom.Participants.RemoteParticipantIdentities())
                foreach ((string sid, TrackPublication pub) in entry.Value.Tracks)
                    if (TryAddStream(pub.Kind, new StreamKey(entry.Key!, sid)))
                        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Added existing remote track from {entry.Key}");

                ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Remote track listening started");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Failed to start listening: {ex.Message}");
                throw;
            }
        }

        internal async UniTaskVoid StopListening()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                foreach (StreamKey key in remoteSources.Keys)
                    RemoveRemoteSource(key);
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to stop listening to remote tracks: {ex.Message}"); }
        }

        internal async UniTaskVoid HandleTrackSubscribed(TrackPublication publication, Participant participant, bool isLocalLoopback = false)
        {
            if (isLocalLoopback && !configuration.EnableLocalTrackPlayback) return; // debug loopback

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                if (TryAddStream(publication.Kind, new StreamKey(participant.Identity, publication.Sid)))
                    ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Track subscribed from {participant.Identity}");
                // ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track loopback enabled (round-trip via server)");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track subscription: {ex.Message}{(isLocalLoopback ? " (local loopback)" : "(new remote)")}"); }
        }

        internal async UniTaskVoid HandleTrackUnsubscribed(TrackPublication publication, Participant participant, bool isLocalLoopback = false)
        {
            if (isLocalLoopback && !configuration.EnableLocalTrackPlayback) return; // debug loopback
            if (publication.Kind != TrackKind.KindAudio) return;

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                RemoveRemoteSource(new StreamKey(participant.Identity, publication.Sid));
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Track unsubscribed from {participant.Identity}{(isLocalLoopback ? " (loopback)" : "(new remote)")}");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Failed to handle track unsubscription: {ex.Message}{(isLocalLoopback ? " (loopback)" : "")}"); }
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

        private bool TryAddStream(TrackKind kind, StreamKey key)
        {
            if (kind != TrackKind.KindAudio) return false;

            Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(key);
            if (!stream.Resource.Has) return false;

            AddRemoteSource(key, stream);
            return true;
        }

        private void AddRemoteSource(StreamKey key, Weak<AudioStream> stream)
        {
            if (remoteSources.TryRemove(key, out LivekitAudioSource? oldSource))
                PlaybackSourcesHub.DisposeSource(oldSource);

            (AudioSource audioSource, LivekitAudioSource source) =
                PlaybackSourcesHub.CreateSource(key, stream, configuration.ProximityChatAudioMixerGroup, fallbackParent, true);

            configuration.ApplyProximitySettingsTo(audioSource);
            configuration.ApplySpatializationSettingsTo(source);
            source.gameObject.AddComponent<ProximityPanCalculator>();

            source.Play();

            if (!remoteSources.TryAdd(key, source))
            {
                ReportHub.LogError(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Cannot add source, key already exists: {key}");
                PlaybackSourcesHub.DisposeSource(source);
                return;
            }

            activeAudioSources[key.identity] = audioSource;

            if (audioSource != null && IsSuppressed)
                audioSource.mute = true;
        }

        private void RemoveRemoteSource(StreamKey key)
        {
            if (remoteSources.TryRemove(key, out LivekitAudioSource? source))
            {
                activeAudioSources.TryRemove(key.identity, out _);
                PlaybackSourcesHub.DisposeSource(source);
            }
        }
    }
}
