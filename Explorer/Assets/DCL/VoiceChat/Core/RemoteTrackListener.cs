using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using RichTypes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Represents listening part, i.e. voice chat "ears".
    ///     - manages remote audio track subscription and playback.
    ///     - debug for listening local microphone (loopback).
    /// </summary>
    public class RemoteTrackListener : IDisposable
    {
        private const string TAG = nameof(RemoteTrackListener);

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly PlaybackSourcesHub playbackSourcesHub;
        private readonly IUserBlockingCache? userBlockingCache;

        private bool isDisposed;

        /// <param name="userBlockingCache">
        /// When supplied, streams from blocked users are skipped in <see cref="TryAddAudioStream"/>.
        /// Pass <c>null</c> for rooms that should hear everyone (Community, private call).
        /// </param>
        public RemoteTrackListener(IRoom voiceChatRoom, VoiceChatConfiguration configuration, PlaybackSourcesHub playbackSourcesHub,
            IUserBlockingCache? userBlockingCache = null)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.playbackSourcesHub = playbackSourcesHub;
            this.userBlockingCache = userBlockingCache;

            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;

            StopListeningAsync().Forget();
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        public async UniTaskVoid StartListeningAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                playbackSourcesHub.Reset();

                foreach (KeyValuePair<string, LKParticipant> remoteParticipantIdentity in voiceChatRoom.Participants.RemoteParticipantIdentities())
                foreach ((string sid, TrackPublication value) in remoteParticipantIdentity.Value.Tracks)
                    if (TryAddAudioStream(value.Kind, new StreamKey(remoteParticipantIdentity.Key!, sid)))
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Added existing remote track from {remoteParticipantIdentity.Key}");

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track listening started");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to start listening to remote tracks: {ex.Message}");
            }
        }

        public async UniTaskVoid StopListeningAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                playbackSourcesHub.Stop();
                playbackSourcesHub.Reset();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track listening stopped");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to stop listening to remote tracks: {ex.Message}"); }
        }

        public async UniTaskVoid HandleTrackSubscribedAsync(TrackPublication publication, LKParticipant participant, bool isLocalLoopback = false)
        {
            if (isLocalLoopback && !configuration.EnableLocalTrackPlayback) return; // debug loopback

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                if (TryAddAudioStream(publication.Kind, new StreamKey(participant.Identity, publication.Sid)))
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Track subscribed from {participant.Identity}{(isLocalLoopback ? " (local loopback)" : " (new remote)")}");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track subscription: {ex.Message}{(isLocalLoopback ? " (local loopback)" : " (new remote)")}"); }
        }

        public async UniTaskVoid HandleTrackUnsubscribedAsync(TrackPublication publication, LKParticipant participant, bool isLocalLoopback = false)
        {
            if (isLocalLoopback && !configuration.EnableLocalTrackPlayback) return; // debug loopback
            if (publication.Kind != TrackKind.KindAudio) return;

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                playbackSourcesHub.TryRemoveStream(new StreamKey(participant.Identity, publication.Sid));
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Track unsubscribed from {participant.Identity}{(isLocalLoopback ? " (loopback)" : " (new remote)")}");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track unsubscription: {ex.Message}{(isLocalLoopback ? " (loopback)" : " (new remote)")}"); }
        }

        internal void RemoveStreamsByIdentity(string identity) =>
            playbackSourcesHub.RemoveStreamsByIdentity(identity);

        /// <summary>
        /// Re-adds playback streams for an identity that was previously removed (e.g. after unblock).
        /// LiveKit keeps the track subscribed, so <see cref="IRoom.TrackSubscribed"/> does not fire again —
        /// we need to look up the participant and re-invoke <see cref="TryAddAudioStream"/> explicitly.
        /// </summary>
        internal async UniTaskVoid AddStreamsForIdentityAsync(string identity)
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                if (!voiceChatRoom.Participants.RemoteParticipantIdentities().TryGetValue(identity, out LKParticipant? participant) || participant == null)
                    return;

                foreach ((string sid, TrackPublication publication) in participant.Tracks)
                    if (TryAddAudioStream(publication.Kind, new StreamKey(identity, sid)))
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Re-added stream for {identity}");
            }
            catch (Exception ex) { ReportHub.LogException(new Exception($"{TAG} Failed to re-add streams for {identity}", ex), ReportCategory.VOICE_CHAT); }
        }

        internal void SetMuteForIdentity(string identity, bool mute) =>
            playbackSourcesHub.SetMuteForIdentity(identity, mute);

        private bool TryAddAudioStream(TrackKind kind, StreamKey key)
        {
            if (kind != TrackKind.KindAudio) return false;
            if (userBlockingCache?.UserIsBlocked(key.identity) == true) return false;

            Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(key);
            if (!stream.Resource.Has) return false;

            playbackSourcesHub.AddOrReplaceStream(key, stream);
            return true;
        }

        // Unity binds AudioSource instances to the output device at creation time; rebuild them on device change so reconnected Bluetooth headphones are heard.
        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!deviceWasChanged || isDisposed) return;
            if (playbackSourcesHub.Streams.Count == 0) return;

            RebuildRemoteSourcesAsync().Forget();
        }

        private async UniTaskVoid RebuildRemoteSourcesAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            if (isDisposed) return;

            try
            {
                var keysToRebuild = new List<StreamKey>(playbackSourcesHub.Streams.Count);
                foreach (KeyValuePair<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> pair in playbackSourcesHub.Streams)
                    keysToRebuild.Add(pair.Key);

                var rebuilt = 0;

                foreach (StreamKey key in keysToRebuild)
                {
                    if (!playbackSourcesHub.Streams.TryGetValue(key, out (Weak<AudioStream> stream, LivekitAudioSource source) entry))
                        continue;

                    playbackSourcesHub.AddOrReplaceStream(key, entry.stream);
                    rebuilt++;
                }

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Audio output device changed — rebuilt {rebuilt} remote audio source(s)");
            }
            catch (Exception ex) { ReportHub.LogException(new Exception($"{TAG} Failed to rebuild audio sources after device change", ex), ReportCategory.VOICE_CHAT); }
        }
    }
}
