using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.Tracks.Hub;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class InteriorRoom : IRoom
    {
        private IRoom? assigned;

        public void Assign(IRoom room, out IRoom? previous)
        {
            previous = assigned;
            if (previous != null)
            {
                previous.RoomMetadataChanged -= RoomOnRoomMetadataChanged;
                previous.LocalTrackPublished -= RoomOnLocalTrackPublished;
                previous.LocalTrackUnpublished -= RoomOnLocalTrackUnpublished;
                previous.TrackPublished -= RoomOnTrackPublished;
                previous.TrackUnpublished -= RoomOnTrackUnpublished;
                previous.TrackSubscribed -= RoomOnTrackSubscribed;
                previous.TrackUnsubscribed -= RoomOnTrackUnsubscribed;
                previous.TrackMuted -= RoomOnTrackMuted;
                previous.TrackUnmuted -= RoomOnTrackUnmuted;
                previous.ConnectionQualityChanged -= RoomOnConnectionQualityChanged;
                previous.ConnectionStateChanged -= RoomOnConnectionStateChanged;
                previous.ConnectionUpdated -= RoomOnConnectionUpdated;
            }

            assigned = room;

            room.RoomMetadataChanged += RoomOnRoomMetadataChanged;
            room.LocalTrackPublished += RoomOnLocalTrackPublished;
            room.LocalTrackUnpublished += RoomOnLocalTrackUnpublished;
            room.TrackPublished += RoomOnTrackPublished;
            room.TrackUnpublished += RoomOnTrackUnpublished;
            room.TrackSubscribed += RoomOnTrackSubscribed;
            room.TrackUnsubscribed += RoomOnTrackUnsubscribed;
            room.TrackMuted += RoomOnTrackMuted;
            room.TrackUnmuted += RoomOnTrackUnmuted;
            room.ConnectionQualityChanged += RoomOnConnectionQualityChanged;
            room.ConnectionStateChanged += RoomOnConnectionStateChanged;
            room.ConnectionUpdated += RoomOnConnectionUpdated;
        }

        private void RoomOnConnectionUpdated(IRoom room, ConnectionUpdate connectionupdate)
        {
            ConnectionUpdated?.Invoke(room, connectionupdate);
        }

        private void RoomOnConnectionStateChanged(ConnectionState connectionstate)
        {
            ConnectionStateChanged?.Invoke(connectionstate);
        }

        private void RoomOnConnectionQualityChanged(ConnectionQuality quality, Participant participant)
        {
            ConnectionQualityChanged?.Invoke(quality, participant);
        }

        private void RoomOnTrackUnmuted(TrackPublication publication, Participant participant)
        {
            TrackUnmuted?.Invoke(publication, participant);
        }

        private void RoomOnTrackMuted(TrackPublication publication, Participant participant)
        {
            TrackMuted?.Invoke(publication, participant);
        }

        private void RoomOnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            TrackUnsubscribed?.Invoke(track, publication, participant);
        }

        private void RoomOnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            TrackSubscribed?.Invoke(track, publication, participant);
        }

        private void RoomOnTrackUnpublished(TrackPublication publication, Participant participant)
        {
            TrackUnpublished?.Invoke(publication, participant);
        }

        private void RoomOnTrackPublished(TrackPublication publication, Participant participant)
        {
            TrackPublished?.Invoke(publication, participant);
        }

        private void RoomOnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            LocalTrackUnpublished?.Invoke(publication, participant);
        }

        private void RoomOnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            LocalTrackPublished?.Invoke(publication, participant);
        }

        private void RoomOnRoomMetadataChanged(string metadata)
        {
            RoomMetadataChanged?.Invoke(metadata);
        }

        public event Room.MetaDelegate? RoomMetadataChanged;
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

        public Task<bool> Connect(string url, string authToken, CancellationToken cancelToken) =>
            assigned.Connect(url, authToken, cancelToken);

        public void Disconnect() =>
            assigned.Disconnect();

        public IActiveSpeakers ActiveSpeakers => assigned.ActiveSpeakers;
        public IParticipantsHub Participants => assigned.Participants;
        public IDataPipe DataPipe => assigned.DataPipe;
    }
}
