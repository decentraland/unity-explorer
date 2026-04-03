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

        private readonly IRoom islandRoom;
        private readonly VoiceChatConfiguration configuration;
        private readonly VoiceChatMicrophoneHandler microphoneHandler;

        private MicrophoneTrack? microphoneTrack;

        internal bool isPublished => microphoneTrack.HasValue;

        internal Weak<MicrophoneRtcAudioSource> currentMicrophone =>
            microphoneTrack?.Source ?? Weak<MicrophoneRtcAudioSource>.Null;

        internal ProximityMicrophoneTrackPublisher(
            IRoom islandRoom,
            VoiceChatConfiguration configuration,
            VoiceChatMicrophoneHandler microphoneHandler)
        {
            this.islandRoom = islandRoom;
            this.configuration = configuration;
            this.microphoneHandler = microphoneHandler;
        }

        public void Dispose()
        {
            Unpublish();
        }

        internal async UniTask PublishAsync(CancellationToken ct)
        {
            MicrophoneRtcAudioSource rtcSource =
                await VoiceChatTrackPublishHelper.CreateMicrophoneSourceAsync(configuration, ct);

            ITrack track = islandRoom.LocalTracks.CreateAudioTrack(
                $"proximity_{islandRoom.Participants.LocalParticipant().Name}",
                rtcSource);

            microphoneTrack = new MicrophoneTrack(track, new Owned<MicrophoneRtcAudioSource>(rtcSource));
            microphoneHandler.AssignProximity(microphoneTrack.Value.Source);

            islandRoom.Participants.LocalParticipant().PublishTrack(
                track, VoiceChatTrackPublishHelper.DEFAULT_PUBLISH_OPTIONS, ct);

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track published");
        }

        internal void Unpublish()
        {
            if (!microphoneTrack.HasValue) return;

            try
            {
                islandRoom.Participants.LocalParticipant().UnpublishTrack(microphoneTrack.Value.Track, true);
                ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Local track unpublished");
            }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.PROXIMITY_VOICE_CHAT, $"{TAG} Error unpublishing: {ex.Message}");
            }

            microphoneHandler.ClearProximity();
            microphoneTrack?.Dispose();
            microphoneTrack = null;
        }
    }
}
