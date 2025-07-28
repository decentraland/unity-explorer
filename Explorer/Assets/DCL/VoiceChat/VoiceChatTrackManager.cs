using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit;
using LiveKit.Audio;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Manages audio track publishing, subscribing, and lifecycle for voice chat.
    /// </summary>
    public class VoiceChatTrackManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatTrackManager);

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatCombinedStreamsAudioSource combinedStreamsAudioSource;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;

        private ITrack microphoneTrack;
        //private OptimizedMonoRtcAudioSource monoRtcAudioSource;
        private MicrophoneRtcAudioSource microphoneRtcAudioSource;
        private CancellationTokenSource trackPublishingCts;
        private bool isDisposed;

        public VoiceChatTrackManager(
            IRoom voiceChatRoom,
            VoiceChatConfiguration configuration,
            VoiceChatCombinedStreamsAudioSource combinedStreamsAudioSource,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.combinedStreamsAudioSource = combinedStreamsAudioSource;
            this.microphoneHandler = microphoneHandler;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            UnpublishLocalTrack();
            StopListeningToRemoteTracks();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        /// <summary>
        ///     Publishes the local microphone track to the room.
        ///     Creates and starts the OptimizedMonoRtcAudioSource if needed.
        /// </summary>
        public void PublishLocalTrack(CancellationToken ct)
        {
            if (microphoneTrack != null)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track already published");
                return;
            }

            try
            {
                microphoneHandler.InitializeMicrophone();
                microphoneRtcAudioSource = microphoneHandler.MicrophoneRtcAudioSource;//new OptimizedMonoRtcAudioSource(microphoneHandler.AudioFilter);
                //microphoneRtcAudioSource.Start();

                microphoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack(
                    voiceChatRoom.Participants.LocalParticipant().Name,
                    microphoneRtcAudioSource);

                var options = new TrackPublishOptions
                {
                    AudioEncoding = new AudioEncoding
                    {
                        MaxBitrate = 124000,
                    },
                    Source = TrackSource.SourceMicrophone,
                };

                voiceChatRoom.Participants.LocalParticipant().PublishTrack(microphoneTrack, options, ct);

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track published successfully");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to publish local track: {ex.Message}");
                CleanupLocalTrack();
                throw;
            }
        }

        public void UnpublishLocalTrack()
        {
            if (microphoneTrack != null)
            {
                try
                {
                    voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack, true);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track unpublished");
                }
                catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to unpublish local track: {ex.Message}"); }
                finally { CleanupLocalTrack(); }
            }
        }

        public void StartListeningToRemoteTracks()
        {
            try
            {
                combinedStreamsAudioSource.Reset();

                foreach (string remoteParticipantIdentity in voiceChatRoom.Participants.RemoteParticipantIdentities())
                {
                    Participant participant = voiceChatRoom.Participants.RemoteParticipant(remoteParticipantIdentity);
                    if (participant == null) continue;

                    foreach ((string sid, TrackPublication value) in participant.Tracks)
                    {
                        if (value.Kind == TrackKind.KindAudio)
                        {
                            WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(remoteParticipantIdentity, sid);

                            if (stream != null)
                            {
                                combinedStreamsAudioSource.AddStream(stream);
                                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Added existing remote track from {remoteParticipantIdentity}");
                            }
                        }
                    }
                }

                combinedStreamsAudioSource.Play();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track listening started");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to start listening to remote tracks: {ex.Message}");
                throw;
            }
        }

        public void StopListeningToRemoteTracks()
        {
            StopListeningToRemoteTracksAsync().Forget();
        }

        private async UniTaskVoid StopListeningToRemoteTracksAsync()
        {
            if (!PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                if (combinedStreamsAudioSource != null)
                {
                    combinedStreamsAudioSource.Stop();
                    combinedStreamsAudioSource.Reset();
                }

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track listening stopped");
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to stop listening to remote tracks: {ex.Message}"); }
        }

        public void HandleTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio)
                {
                    WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                    if (stream != null)
                    {
                        combinedStreamsAudioSource.AddStream(stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} New remote track subscribed from {participant.Identity}");
                    }
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track subscription: {ex.Message}"); }
        }

        public void HandleTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio)
                {
                    WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                    if (stream != null)
                    {
                        combinedStreamsAudioSource.RemoveStream(stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track unsubscribed from {participant.Identity}");
                    }
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle track unsubscription: {ex.Message}"); }
        }

        public void HandleLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio && configuration.EnableLocalTrackPlayback)
                {
                    WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                    if (stream != null)
                    {
                        combinedStreamsAudioSource.AddStream(stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track added to playback (loopback enabled)");
                    }
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track published: {ex.Message}"); }
        }

        public void HandleLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            try
            {
                if (publication.Kind == TrackKind.KindAudio && configuration.EnableLocalTrackPlayback)
                {
                    WeakReference<IAudioStream> stream = voiceChatRoom.AudioStreams.ActiveStream(participant.Identity, publication.Sid);

                    if (stream != null)
                    {
                        combinedStreamsAudioSource.RemoveStream(stream);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Local track removed from playback");
                    }
                }
            }
            catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to handle local track unpublished: {ex.Message}"); }
        }

        private void CleanupLocalTrack()
        {
            microphoneTrack = null;
            microphoneRtcAudioSource?.Stop();
            //monoRtcAudioSource = null;
            trackPublishingCts?.SafeCancelAndDispose();
            trackPublishingCts = null;
        }
    }
}
