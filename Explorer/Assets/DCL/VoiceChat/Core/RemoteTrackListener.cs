using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using RichTypes;
using System;
using System.Collections.Generic;
using AudioStreamInfo = LiveKit.Rooms.Streaming.Audio.AudioStreamInfo;

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

        private bool isDisposed;
        internal bool isSuppressed { get; private set; }

        public IReadOnlyDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> RemoteStreams => playbackSourcesHub.Streams;

        public RemoteTrackListener(IRoom voiceChatRoom, VoiceChatConfiguration configuration, PlaybackSourcesHub playbackSourcesHub)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.playbackSourcesHub = playbackSourcesHub;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            StopListeningAsync().Forget();
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        public void ActiveStreamsInfo(List<StreamInfo<AudioStreamInfo>> output) =>
            voiceChatRoom.AudioStreams.ListInfo(output);

        public async UniTaskVoid StartListening()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                playbackSourcesHub.Reset();

                foreach (KeyValuePair<string, Participant> remoteParticipantIdentity in voiceChatRoom.Participants.RemoteParticipantIdentities())
                foreach ((string sid, TrackPublication value) in remoteParticipantIdentity.Value.Tracks)
                    if (TryAddStream(value.Kind, new StreamKey(remoteParticipantIdentity.Key!, sid)))
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Added existing remote track from {remoteParticipantIdentity.Key}");

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track listening started");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to start listening to remote tracks: {ex.Message}");
                throw;
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

        public async UniTaskVoid HandleTrackSubscribed(TrackPublication publication, Participant participant, bool isLocalLoopback = false)
        {
            if (isLocalLoopback && !configuration.EnableLocalTrackPlayback) return; // debug loopback

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                if (TryAddStream(publication.Kind, new StreamKey(participant.Identity, publication.Sid)))
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Track subscribed from {participant.Identity}{(isLocalLoopback ? " (local loopback)" : "(new remote)")}");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track subscription: {ex.Message}{(isLocalLoopback ? " (local loopback)" : "(new remote)")}"); }
        }

        public async UniTaskVoid HandleTrackUnsubscribed(TrackPublication publication, Participant participant, bool isLocalLoopback = false)
        {
            if (isLocalLoopback && !configuration.EnableLocalTrackPlayback) return; // debug loopback
            if (publication.Kind != TrackKind.KindAudio) return;

            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                playbackSourcesHub.TryRemoveStream(new StreamKey(participant.Identity, publication.Sid));
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Track unsubscribed from {participant.Identity}{(isLocalLoopback ? " (loopback)" : "(new remote)")}");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track unsubscription: {ex.Message}{(isLocalLoopback ? " (loopback)" : "(new remote)")}"); }
        }

        internal void MuteAll()
        {
            if (isSuppressed) return;
            isSuppressed = true;
            playbackSourcesHub.SetMuteAll(true);
        }

        internal void UnmuteAll()
        {
            if (!isSuppressed) return;
            isSuppressed = false;
            playbackSourcesHub.SetMuteAll(false);
        }

        private bool TryAddStream(TrackKind kind, StreamKey key)
        {
            if (kind != TrackKind.KindAudio) return false;

            Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(key);
            if (!stream.Resource.Has) return false;

            playbackSourcesHub.AddOrReplaceStream(key, stream);
            return true;
        }
    }
}
