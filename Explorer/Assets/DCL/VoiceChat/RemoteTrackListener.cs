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

        public IReadOnlyDictionary<StreamKey, (Weak<AudioStream> stream, LivekitAudioSource source)> RemoteStreams => playbackSourcesHub.Streams;

        public RemoteTrackListener(IRoom voiceChatRoom, VoiceChatConfiguration configuration)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;

            playbackSourcesHub = new PlaybackSourcesHub(configuration.ChatAudioMixerGroup.EnsureNotNull());
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

        public void StartListening()
        {
            try
            {
                playbackSourcesHub.Reset();

                foreach (KeyValuePair<string, Participant> remoteParticipantIdentity in voiceChatRoom.Participants.RemoteParticipantIdentities())
                foreach ((string sid, TrackPublication value) in remoteParticipantIdentity.Value.Tracks)
                {
                    if (value.Kind != TrackKind.KindAudio) continue;

                    Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(new StreamKey(remoteParticipantIdentity.Key!, sid));
                    if (stream.Resource.Has)
                    {
                        playbackSourcesHub.AddOrReplaceStream(new StreamKey(remoteParticipantIdentity.Key!, sid), stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Added existing remote track from {remoteParticipantIdentity}");
                    }
                }

                playbackSourcesHub.Play();
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

        public void HandleTrackSubscribed(TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio)
                {
                    Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(new StreamKey(participant.Identity, publication.Sid));

                    if (stream.Resource.Has)
                    {
                        playbackSourcesHub.AddOrReplaceStream(new StreamKey(participant.Identity, publication.Sid), stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} New remote track subscribed from {participant.Identity}");
                    }
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track subscription: {ex.Message}"); }
        }

        public void HandleTrackUnsubscribed(TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio)
                {
                    playbackSourcesHub.RemoveStream(new StreamKey(participant.Identity, publication.Sid));
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track unsubscribed from {participant.Identity}");
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track unsubscription: {ex.Message}"); }
        }

#region DEBUG METHODS
        public void HandleLoopbackTrackPublished(TrackPublication publication, Participant participant)
        {
            try
            {
                if (!configuration.EnableLocalTrackPlayback || publication.Kind != TrackKind.KindAudio) return;

                Weak<AudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(new StreamKey(participant.Identity, publication.Sid));
                if (stream.Resource.Has)
                {
                    playbackSourcesHub.AddOrReplaceStream(new StreamKey(participant.Identity, publication.Sid), stream);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track added to playback (loopback enabled)");
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track published: {ex.Message}"); }
        }

        public void HandleLoopbackTrackUnpublished(TrackPublication publication, Participant participant)
        {
            try
            {
                if (!configuration.EnableLocalTrackPlayback || publication.Kind != TrackKind.KindAudio) return;

                playbackSourcesHub.RemoveStream(new StreamKey(participant.Identity, publication.Sid));
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track removed from playback");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track unpublished: {ex.Message}"); }
        }
#endregion
    }
}
