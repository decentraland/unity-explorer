using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;

#if !UNITY_WEBGL || UNITY_EDITOR
using LiveKit.Rooms.Streaming.Audio;
#endif

using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.Tracks.Hub;
using LiveKit.Rooms.VideoStreaming;
using System.Threading;
using Cysharp.Threading.Tasks;
using RichTypes;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class NullRoom : IRoom
    {
        public static readonly NullRoom INSTANCE = new ();

        public IActiveSpeakers ActiveSpeakers => NullActiveSpeakers.INSTANCE;
        public IParticipantsHub Participants => NullParticipantsHub.INSTANCE;
        public IDataPipe DataPipe => NullDataPipe.INSTANCE;
        public IRoomInfo Info => NullRoomInfo.INSTANCE;

#if !UNITY_WEBGL || UNITY_EDITOR
        public IVideoStreams VideoStreams => NullVideoStreams.INSTANCE;
        public IAudioStreams AudioStreams => NullAudioStreams.INSTANCE;
        public ILocalTracks LocalTracks => NullLocalTracks.INSTANCE;

        public event LocalPublishDelegate? LocalTrackPublished;
        public event LocalPublishDelegate? LocalTrackUnpublished;
        public event PublishDelegate? TrackPublished;
        public event PublishDelegate? TrackUnpublished;
        public event SubscribeDelegate? TrackSubscribed;
        public event SubscribeDelegate? TrackUnsubscribed;
        public event MuteDelegate? TrackMuted;
        public event MuteDelegate? TrackUnmuted;
#endif

        public event ConnectionQualityChangeDelegate? ConnectionQualityChanged;
        public event ConnectionStateChangeDelegate? ConnectionStateChanged;
        public event ConnectionDelegate? ConnectionUpdated;
        public event Room.MetaDelegate? RoomMetadataChanged;
        public event Room.SidDelegate? RoomSidChanged;

        public void UpdateLocalMetadata(string metadata)
        {
            //ignore
        }

        public void SetLocalName(string name) { }

        public UniTask<Result> ConnectAsync(string url, string authToken, CancellationToken cancelToken, bool autoSubscribe) =>
            UniTask.FromResult(Result.SuccessResult());

        public UniTask DisconnectAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
    }
}
