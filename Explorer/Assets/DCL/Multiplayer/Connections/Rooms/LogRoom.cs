using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms.Logs;
using LiveKit;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.Tracks.Hub;
using LiveKit.Rooms.VideoStreaming;
using LiveKit.RtcSources.Video;
using System.Threading;
using System.Threading.Tasks;
using RichTypes;
using System;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class LogRoom : IRoom
    {
        private const string PREFIX_BASE = "LogRoom:";
        private readonly string prefix;

        private readonly IRoom origin;

        public IActiveSpeakers ActiveSpeakers { get; }
        public IParticipantsHub Participants { get; }
        public IDataPipe DataPipe { get; }
        public IRoomInfo Info { get; }
        public IVideoStreams VideoStreams { get; }
        public IAudioStreams AudioStreams { get; }
        public ILocalTracks LocalTracks { get; }

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

        public event Room.SidDelegate? RoomSidChanged;

        public LogRoom() : this(new Room(), "default") { }

        public LogRoom(IRoom origin, string roomName)
        {
            prefix = $"{PREFIX_BASE} {roomName}:";

            this.origin = origin;

            ActiveSpeakers = new LogActiveSpeakers(origin.ActiveSpeakers);
            Participants = new LogParticipantsHub(origin.Participants);
            DataPipe = new LogDataPipe(origin.DataPipe);
            Info = new LogRoomInfo(origin.Info);
            VideoStreams = new LogVideoStreams(origin.VideoStreams);
            AudioStreams = new LogAudioStreams(origin.AudioStreams);
            LocalTracks = new LogLocalTracks(origin.LocalTracks);

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
            this.origin.RoomSidChanged += OriginOnRoomSidChanged;
        }

        private void OriginOnRoomSidChanged(string sid)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} room sid changed {sid}");

            RoomSidChanged?.Invoke(sid);
        }

        private void OriginOnRoomMetadataChanged(string metadata)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} room metadata changed {metadata}");

            RoomMetadataChanged?.Invoke(metadata);
        }

        private void OriginOnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} connection updated {connectionUpdate}");

            ConnectionUpdated?.Invoke(room, connectionUpdate, disconnectReason);
        }

        private void OriginOnConnectionStateChanged(ConnectionState connectionState)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} connection state changed {connectionState}");

            ConnectionStateChanged?.Invoke(connectionState);
        }

        private void OriginOnConnectionQualityChanged(ConnectionQuality quality, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} connection quality changed {quality} by {participant.Sid} {participant.Name}");

            ConnectionQualityChanged?.Invoke(quality, participant);
        }

        private void OriginOnTrackUnmuted(TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} track unmuted {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            TrackUnmuted?.Invoke(publication, participant);
        }

        private void OriginOnTrackMuted(TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} track muted {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            TrackMuted?.Invoke(publication, participant);
        }

        private void OriginOnTrackUnsubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} track unsubscribed {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            TrackUnsubscribed?.Invoke(track, publication, participant);
        }

        private void OriginOnTrackSubscribed(ITrack track, TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} track subscribed {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            TrackSubscribed?.Invoke(track, publication, participant);
        }

        private void OriginOnLocalTrackPublished(TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} local track published {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            LocalTrackPublished?.Invoke(publication, participant);
        }

        private void OriginOnTrackUnpublished(TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} track unpublished {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            TrackUnpublished?.Invoke(publication, participant);
        }

        private void OriginOnLocalTrackUnpublished(TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} local track unpublished {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            LocalTrackUnpublished?.Invoke(publication, participant);
        }

        private void OriginOnTrackPublished(TrackPublication publication, Participant participant)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} track published {publication.Sid} {publication.Kind} by {participant.Sid} {participant.Name}");

            TrackPublished?.Invoke(publication, participant);
        }

        public void UpdateLocalMetadata(string metadata)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} update local metadata: '{metadata}'");

            origin.UpdateLocalMetadata(metadata);
        }

        public void SetLocalName(string name)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} set local name: '{name}'");

            origin.SetLocalName(name);
        }

        public async Task<Result> ConnectAsync(string url, string authToken, CancellationToken cancelToken, bool autoSubscribe)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} connect start {url} with token {authToken}");

            Result result = await origin.ConnectAsync(url, authToken, cancelToken, autoSubscribe);

            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} connect start {url} with token {authToken} with result {result}");

            return result;
        }

        public async Task DisconnectAsync(CancellationToken token)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} disconnect start");

            await origin.DisconnectAsync(token);

            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{prefix} disconnect end");
        }
    }
}
