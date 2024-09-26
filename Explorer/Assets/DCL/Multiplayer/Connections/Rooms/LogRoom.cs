using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms.Logs;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.Tracks.Hub;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class LogRoom : IRoom
    {
        private const string PREFIX = "LogRoom:";

        private readonly IRoom origin;

        public IActiveSpeakers ActiveSpeakers { get; }
        public IParticipantsHub Participants { get; }
        public IDataPipe DataPipe { get; }
        public IRoomInfo Info { get; }

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

        public LogRoom() : this(new Room()) { }

        public LogRoom(IRoom origin)
        {
            this.origin = origin;

            ActiveSpeakers = new LogActiveSpeakers(origin.ActiveSpeakers);
            Participants = new LogParticipantsHub(origin.Participants);
            DataPipe = new LogDataPipe(origin.DataPipe);
            Info = new LogRoomInfo(origin.Info);

            this.origin.LocalTrackPublished += OriginOnLocalTrackPublished;
            this.origin.LocalTrackUnpublished += OriginOnLocalTrackUnpublished;
            this.origin.TrackPublished += OriginOnTrackPublished;
            this.origin.TrackUnpublished += OriginOnTrackUnpublished;
            this.origin.TrackSubscribed += OriginOnTrackSubscribed;
            this.origin.TrackUnsubscribed += OriginOnTrackUnsubscribed;
            this.origin.TrackMuted += OriginOnTrackMuted;
            this.origin.TrackUnmuted += OriginOnTrackUnmuted;
            this.origin.ConnectionQualityChanged += OriginOnConnectionQualityChanged;
            this.origin.ConnectionStateChanged += OriginOnConnectionStateChanged;
            this.origin.ConnectionUpdated += OriginOnConnectionUpdated;
            this.origin.RoomMetadataChanged += OriginOnRoomMetadataChanged;
        }

        private void OriginOnRoomMetadataChanged(string metadata)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} room metadata changed {metadata}");
            RoomMetadataChanged?.Invoke(metadata);
        }

        private void OriginOnConnectionUpdated(IRoom room, ConnectionUpdate connectionupdate)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} connection updated {connectionupdate}");
            ConnectionUpdated?.Invoke(room, connectionupdate);
        }

        private void OriginOnConnectionStateChanged(ConnectionState connectionstate)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} connection state changed {connectionstate}");
            ConnectionStateChanged?.Invoke(connectionstate);
        }

        private void OriginOnConnectionQualityChanged(ConnectionQuality quality, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} connection quality changed {quality} by {participant.Sid} {participant.Name}");
            ConnectionQualityChanged?.Invoke(quality, participant);
        }

        private void OriginOnTrackUnmuted(TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} track unmuted {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            TrackUnmuted?.Invoke(publication, participant);
        }

        private void OriginOnTrackMuted(TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} track muted {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            TrackMuted?.Invoke(publication, participant);
        }

        private void OriginOnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} track unsubscribed {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            TrackUnsubscribed?.Invoke(track, publication, participant);
        }

        private void OriginOnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} track subscribed {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            TrackSubscribed?.Invoke(track, publication, participant);
        }

        private void OriginOnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} local track published {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            LocalTrackPublished?.Invoke(publication, participant);
        }

        private void OriginOnTrackUnpublished(TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} track unpublished {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            TrackUnpublished?.Invoke(publication, participant);
        }

        private void OriginOnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} local track unpublished {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            LocalTrackUnpublished?.Invoke(publication, participant);
        }

        private void OriginOnTrackPublished(TrackPublication publication, Participant participant)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} track published {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");
            TrackPublished?.Invoke(publication, participant);
        }

        public void UpdateLocalMetadata(string metadata)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} update local metadata: '{metadata}'");
            origin.UpdateLocalMetadata(metadata);
        }

        public void SetLocalName(string name)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} set local name: '{name}'");
            origin.SetLocalName(name);
        }

        public async Task<bool> ConnectAsync(string url, string authToken, CancellationToken cancelToken, bool autoSubscribe)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} connect start {url} with token {authToken}");
            bool result = await origin.ConnectAsync(url, authToken, cancelToken, autoSubscribe);
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} connect start {url} with token {authToken} with result {result}");
            return result;
        }

        public async Task DisconnectAsync(CancellationToken token)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} disconnect start");
            await origin.DisconnectAsync(token);
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{PREFIX} disconnect end");
        }
    }
}
