using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using LiveKit.Audio;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using RichTypes;
using System;
using System.Threading;
using Utility.Multithreading;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Manages local microphone track publishing lifecycle.
    ///     Used for both community and proximity voice chat.
    /// </summary>
    public class MicrophoneTrackPublisher : IDisposable
    {
        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly VoiceChatType voiceChatType;
        private readonly string tag;

        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);

        private MicrophoneTrack? microphoneTrack;

        public Weak<MicrophoneRtcAudioSource> CurrentMicrophone => microphoneTrack?.Source ?? Weak<MicrophoneRtcAudioSource>.Null;
        internal bool isPublished => microphoneTrack.HasValue;

        private bool isDisposed;

        public MicrophoneTrackPublisher(
            IRoom voiceChatRoom,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneHandler microphoneHandler,
            VoiceChatType voiceChatType)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.microphoneHandler = microphoneHandler;
            this.voiceChatType = voiceChatType;
            tag = $"{nameof(MicrophoneTrackPublisher)}({voiceChatType})";
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            Unpublish();
            semaphoreSlim.Dispose();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Disposed");
        }

        /// <summary>
        ///     Publishes the local microphone track to the room.
        ///     Creates the <see cref="MicrophoneRtcAudioSource"/> via shared helper;
        ///     conditionally starts it based on <paramref name="micAutoStart"/> and current mic-enabled state.
        /// </summary>
        public async UniTask PublishAsync(bool micAutoStart, CancellationToken ct)
        {
            using var _ = await semaphoreSlim.LockAsync();

            if (microphoneTrack.HasValue)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Local track already published");
                return;
            }

            try
            {
                MicrophoneRtcAudioSource rtcAudioSource =
                    await VoiceChatTrackPublishHelper.CreateMicrophoneSourceAsync(configuration, ct);

                if (micAutoStart && microphoneHandler.IsMicrophoneEnabled.Value)
                    rtcAudioSource.Start();

                ITrack track = voiceChatRoom.LocalTracks.CreateAudioTrack(
                    voiceChatRoom.Participants.LocalParticipant().Name, rtcAudioSource);

                microphoneTrack = new MicrophoneTrack(track, new Owned<MicrophoneRtcAudioSource>(rtcAudioSource));
                microphoneHandler.Assign(microphoneTrack.Value.Source, voiceChatType);

                voiceChatRoom.Participants.LocalParticipant().PublishTrack(
                    track, VoiceChatTrackPublishHelper.DEFAULT_PUBLISH_OPTIONS, ct);

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Local track published successfully");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{tag} Failed to publish local track: {ex.Message}");
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification("No Available Microphone"));
                CleanupLocalTrack();
                throw;
            }
        }

        public void Unpublish()
        {
            if (microphoneTrack.HasValue)
                try
                {
                    voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack.Value.Track, true);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"{tag} Local track unpublished");
                }
                catch (Exception ex) { ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{tag} Failed to unpublish: {ex.Message}"); }
                finally { CleanupLocalTrack(); }
        }

        private void CleanupLocalTrack()
        {
            microphoneHandler.ClearSource(voiceChatType);
            microphoneTrack?.Dispose();
            microphoneTrack = null;
        }
    }
}
