using DCL.Multiplayer.Connections.Rooms.Interior;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.Tracks.Hub;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class InteriorRoom : IRoom, IInterior<IRoom>
    {
        private readonly InteriorActiveSpeakers activeSpeakers = new ();
        private readonly InteriorParticipantsHub participants = new ();
        private readonly InteriorDataPipe dataPipe = new ();
        private IRoom assigned = NullRoom.INSTANCE;

        public IActiveSpeakers ActiveSpeakers => activeSpeakers;
        public IParticipantsHub Participants => participants;
        public IDataPipe DataPipe => dataPipe;
        public IRoomInfo Info => assigned.Info;

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

        public void Assign(IRoom room, out IRoom? previous)
        {
            previous = assigned;

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

            previous = previous is NullRoom ? null : previous;

            assigned = room;

            activeSpeakers.Assign(room.ActiveSpeakers);
            participants.Assign(room.Participants);
            dataPipe.Assign(room.DataPipe);

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

        public void UpdateLocalMetadata(string metadata) =>
            assigned.UpdateLocalMetadata(metadata);

        public Task<bool> ConnectAsync(string url, string authToken, CancellationToken cancelToken, bool autoSubscribe) =>
            assigned.EnsureAssigned().ConnectAsync(url, authToken, cancelToken, autoSubscribe);

        public Task DisconnectAsync(CancellationToken token) =>
            assigned.EnsureAssigned().DisconnectAsync(token);
    }
}
