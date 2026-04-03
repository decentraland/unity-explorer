using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Audio;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using RichTypes;
using System;
using System.Threading;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    ///     Manages local microphone track publishing lifecycle for proximity voice chat.
    ///     Uses <see cref="VoiceChatTrackPublishHelper"/> for source creation
    ///     and delegates mic control to <see cref="VoiceChatMicrophoneHandler"/> via Weak reference.
    /// </summary>
    internal class ProximityMicrophoneTrackPublisher : IDisposable
    {
        private const string TAG = nameof(ProximityMicrophoneTrackPublisher);

        private readonly IRoom voiceChatRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;

        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);

        private MicrophoneTrack? microphoneTrack;

        internal Weak<MicrophoneRtcAudioSource> СurrentMicrophone => microphoneTrack?.Source ?? Weak<MicrophoneRtcAudioSource>.Null;
        internal bool isPublished => microphoneTrack.HasValue;

        private bool isDisposed;

        internal ProximityMicrophoneTrackPublisher(
            IRoom voiceChatRoom,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.configuration = configuration;
            this.microphoneHandler = microphoneHandler;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            Unpublish();
            semaphoreSlim.Dispose();

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Disposed");
        }

        /// <summary>
        ///     Publishes the local microphone track to the room.
        ///     Creates the <see cref="MicrophoneRtcAudioSource"/> via shared helper;
        ///     conditionally starts it based on current mic-enabled state.
        /// </summary>
        internal async UniTask PublishAsync(CancellationToken ct)
        {
            MicrophoneRtcAudioSource rtcAudioSource =
                await VoiceChatTrackPublishHelper.CreateMicrophoneSourceAsync(configuration, ct);

            ITrack track = voiceChatRoom.LocalTracks.CreateAudioTrack(
                voiceChatRoom.Participants.LocalParticipant().Name, rtcAudioSource);

            microphoneTrack = new MicrophoneTrack(track, new Owned<MicrophoneRtcAudioSource>(rtcAudioSource));
            microphoneHandler.Assign(microphoneTrack.Value.Source, VoiceChatType.PROXIMITY);

            voiceChatRoom.Participants.LocalParticipant().PublishTrack(
                track, VoiceChatTrackPublishHelper.DEFAULT_PUBLISH_OPTIONS, ct);

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track published successfully");
        }

        internal void Unpublish()
        {
            if (!microphoneTrack.HasValue) return;

            try
            {
                voiceChatRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack.Value.Track, true);
                ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track unpublished");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Error unpublishing: {ex.Message}");
            }

            microphoneHandler.ClearSource(VoiceChatType.PROXIMITY);
            microphoneTrack?.Dispose();
            microphoneTrack = null;
        }
    }
}
