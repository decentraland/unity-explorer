using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using LiveKit;
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
    /// Manages audio track publishing, subscribing, and lifecycle for voice chat.
    /// </summary>
    public class VoiceChatTrackManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatTrackManager);
        
        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatCombinedStreamsAudioSource combinedStreamsAudioSource;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        
        private ITrack microphoneTrack;
        private OptimizedMonoRtcAudioSource monoRtcAudioSource;
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
            
            voiceChatRoom.LocalTrackPublished += OnLocalTrackPublished;
            voiceChatRoom.LocalTrackUnpublished += OnLocalTrackUnpublished;
        }

        /// <summary>
        /// Publishes the local microphone track to the room.
        /// Creates and starts the OptimizedMonoRtcAudioSource if needed.
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
                monoRtcAudioSource = new OptimizedMonoRtcAudioSource(microphoneHandler.AudioFilter);
                monoRtcAudioSource.Start();
                
                microphoneTrack = voiceChatRoom.AudioTracks.CreateAudioTrack(
                    voiceChatRoom.Participants.LocalParticipant().Name, 
                    monoRtcAudioSource);

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
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to unpublish local track: {ex.Message}");
                }
                finally
                {
                    CleanupLocalTrack();
                }
            }
        }

        /// <summary>
        /// Subscribes to all existing remote audio tracks and sets up event handlers for new tracks.
        /// </summary>
        public void SubscribeToRemoteTracks()
        {
            try
            {
                combinedStreamsAudioSource.Reset();

                // Subscribe to existing remote tracks
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

                // Set up event handlers for future track subscriptions
                voiceChatRoom.TrackSubscribed += OnTrackSubscribed;
                voiceChatRoom.TrackUnsubscribed += OnTrackUnsubscribed;
                
                combinedStreamsAudioSource.Play();
                
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track subscription setup completed");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to subscribe to remote tracks: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unsubscribes from all remote tracks and cleans up event handlers.
        /// </summary>
        public void UnsubscribeFromRemoteTracks()
        {
            try
            {
                voiceChatRoom.TrackSubscribed -= OnTrackSubscribed;
                voiceChatRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
                
                if (combinedStreamsAudioSource != null)
                {
                    combinedStreamsAudioSource.Stop();
                    combinedStreamsAudioSource.Reset();
                }
                
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Remote track unsubscription completed");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to unsubscribe from remote tracks: {ex.Message}");
            }
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
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

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
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

        private void OnLocalTrackPublished(TrackPublication publication, Participant participant)
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

        private void OnLocalTrackUnpublished(TrackPublication publication, Participant participant)
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

        private void CleanupLocalTrack()
        {
            microphoneTrack = null;
            monoRtcAudioSource?.Stop();
            monoRtcAudioSource = null;
            trackPublishingCts?.SafeCancelAndDispose();
            trackPublishingCts = null;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            
            voiceChatRoom.LocalTrackPublished -= OnLocalTrackPublished;
            voiceChatRoom.LocalTrackUnpublished -= OnLocalTrackUnpublished;

            UnpublishLocalTrack();
            UnsubscribeFromRemoteTracks();
            
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }
    }
} 