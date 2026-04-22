using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Utilities;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using RichTypes;
using System;
using System.Collections.Generic;

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
        private readonly ObjectProxy<IUserBlockingCache>? blockingCacheProxy;

        private bool isDisposed;

        /// <param name="blockingCacheProxy">
        /// When supplied, streams from blocked users are skipped in <see cref="TryAddStream"/>.
        /// Pass <c>null</c> for rooms that should hear everyone (Community, private call).
        /// </param>
        public RemoteTrackListener(IRoom voiceChatRoom, VoiceChatConfiguration configuration, PlaybackSourcesHub playbackSourcesHub,
            ObjectProxy<IUserBlockingCache>? blockingCacheProxy = null)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.playbackSourcesHub = playbackSourcesHub;
            this.blockingCacheProxy = blockingCacheProxy;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

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
                    if (TryAddStream(value.Kind, new StreamKey(remoteParticipantIdentity.Key!, sid)))
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
                if (TryAddStream(publication.Kind, new StreamKey(participant.Identity, publication.Sid)))
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
        /// we need to look up the participant and re-invoke <see cref="TryAddStream"/> explicitly.
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
                    if (TryAddStream(publication.Kind, new StreamKey(identity, sid)))
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Re-added stream for {identity}");
            }
            catch (Exception ex) { ReportHub.LogException(new Exception($"{TAG} Failed to re-add streams for {identity}", ex), ReportCategory.VOICE_CHAT); }
        }

        internal void SetMuteForIdentity(string identity, bool mute) =>
            playbackSourcesHub.SetMuteForIdentity(identity, mute);

        private bool TryAddStream(TrackKind kind, StreamKey key)
        {
            if (kind != TrackKind.KindAudio) return false;
            if (blockingCacheProxy?.Object?.UserIsBlocked(key.identity) == true) return false;

            Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(key);
            if (!stream.Resource.Has) return false;

            playbackSourcesHub.AddOrReplaceStream(key, stream);
            return true;
        }
    }
}
