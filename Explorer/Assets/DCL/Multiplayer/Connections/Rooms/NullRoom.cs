using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Tracks.Hub;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class NullRoom : IRoom
    {
        public static readonly NullRoom INSTANCE = new ();

        public IActiveSpeakers ActiveSpeakers => NullActiveSpeakers.INSTANCE;
        public IParticipantsHub Participants => NullParticipantsHub.INSTANCE;
        public IDataPipe DataPipe => NullDataPipe.INSTANCE;
        public IRoomInfo Info => NullRoomInfo.INSTANCE;

        public event LocalPublishDelegate? LocalTrackPublished;
        public event LocalPublishDelegate? LocalTrackUnpublished;
        public event PublishDelegate? TrackPublished;
        public event PublishDelegate? TrackUnpublished;
        public event SubscribeDelegate? TrackSubscribed;
        public event SubscribeDelegate? TrackUnsubscribed;
        public event MuteDelegate? TrackMuted;
        public event MuteDelegate? TrackUnmuted;
        public event ConnectionQualityChangeDelegate? ConnectionQualityChanged;
        public event ConnectionStateChangeDelegate? ConnectionStateChanged;
        public event ConnectionDelegate? ConnectionUpdated;
        public event Room.MetaDelegate? RoomMetadataChanged;

        public Task<bool> Connect(string url, string authToken, CancellationToken cancelToken, bool autoSubscribe) =>
            Task.FromResult(true);

        public Task<bool> Connect(string url, string authToken, CancellationToken cancelToken) =>
            Task.FromResult(true);

        public void Disconnect()
        {
            //ignore
        }
    }
}
