using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Rooms.Interior;
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
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Pool;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class InteriorRoom : IRoom, IInterior<IRoom>
    {
        private readonly InteriorActiveSpeakers activeSpeakers = new ();
        private readonly InteriorParticipantsHub participants = new ();
        private readonly InteriorDataPipe dataPipe = new ();
        private readonly InteriorVideoStreams videoStreams = new ();
        private readonly InteriorAudioStreams audioStreams = new ();
        private readonly InteriorAudioTracks audioTracks = new ();

        private const int RESET_ROOM_TIMEOUT_SECONDS = 5;

        public IActiveSpeakers ActiveSpeakers => activeSpeakers;
        public IParticipantsHub Participants => participants;
        public IDataPipe DataPipe => dataPipe;
        public IRoomInfo Info => assigned.Info;
        public IVideoStreams VideoStreams => videoStreams;
        public IAudioStreams AudioStreams => audioStreams;
        public IAudioTracks AudioTracks => audioTracks;

        internal IRoom assigned { get; private set; } = NullRoom.INSTANCE;

        public event Room.MetaDelegate? RoomMetadataChanged;
        public event Room.SidDelegate? RoomSidChanged;
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
        public event DisconnectionDelegate? Disconnected;

        /// <summary>
        ///     It's not safe to call this method as the previous room can be "forgotten" without notifications
        /// </summary>
        public void Assign(IRoom room, out IRoom? previous)
        {
            if (assigned is { Info: { ConnectionState: ConnectionState.ConnConnected or ConnectionState.ConnReconnecting } })
                ReportHub.LogError(ReportCategory.LIVEKIT, "Assigning a new room without disconnecting the previous one");

            previous = assigned;

            Unsubscribe(previous);

            previous = previous is NullRoom ? null : previous;

            assigned = room;

            Subscribe(assigned);
        }

        /// <summary>
        ///     Disconnects from the current room and connects to the <see cref="NullRoom" />
        /// </summary>
        public UniTask ResetRoom(IObjectPool<IRoom> roomsPool, CancellationToken ct) =>
            SwapRoomsAsync(RoomSelection.NEW, assigned, NullRoom.INSTANCE, roomsPool, ct);

        /// <summary>
        ///     Disconnects from the current room and connects to the <see cref="NullRoom" /> without using the RoomPool
        /// </summary>
        public async UniTask ResetRoomAsync(CancellationToken ct)
        {
            var disconnectTask = assigned.DisconnectAsync(ct).AsUniTask();
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(RESET_ROOM_TIMEOUT_SECONDS), cancellationToken: ct);
            var winIndex = await UniTask.WhenAny(disconnectTask, timeoutTask);
            if (winIndex != 0)
            {
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"ResetRoomAsync timed out after {RESET_ROOM_TIMEOUT_SECONDS} seconds");
            }
            Unsubscribe(assigned);
            assigned = NullRoom.INSTANCE;
        }

        internal async UniTask SwapRoomsAsync(RoomSelection roomSelection, IRoom previous, IRoom newRoom, IObjectPool<IRoom> roomsPool, CancellationToken ct)
        {
            switch (roomSelection)
            {
                case RoomSelection.NEW:
                    // Disconnect the previous room, but make its callbacks pass through
                    try { await previous.DisconnectAsync(ct); }
                    finally
                    {
                        Unsubscribe(previous);

                        assigned = newRoom;

                        if (previous is not NullRoom)
                            roomsPool.Release(previous);

                        Subscribe(newRoom);

                        // During the connection we skipped the connection callback, so we need to notify the subscribers
                        if (newRoom is not NullRoom)
                            SimulateConnectionStateChanged();
                    }

                    break;
                case RoomSelection.PREVIOUS:
                    // drop the new room
                    await newRoom.DisconnectAsync(ct);

                    // don't change the assigned room
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(roomSelection));
            }
        }

        public void SimulateConnectionStateChanged()
        {
            // It's not clear why LiveKit has two different events for the same thing
            ConnectionState currentState = assigned.Info.ConnectionState;

            ConnectionUpdate connectionUpdate = currentState switch
                                                {
                                                    ConnectionState.ConnConnected => ConnectionUpdate.Connected,
                                                    ConnectionState.ConnDisconnected => ConnectionUpdate.Disconnected,
                                                    ConnectionState.ConnReconnecting => ConnectionUpdate.Reconnecting,
                                                    _ => throw new ArgumentOutOfRangeException(),
                                                };

            // TODO check the order of these messages
            ConnectionUpdated?.Invoke(assigned, connectionUpdate);
            ConnectionStateChanged?.Invoke(currentState);
        }

        private void Subscribe(IRoom room)
        {
            activeSpeakers.Assign(room.ActiveSpeakers);
            participants.Assign(room.Participants);
            dataPipe.Assign(room.DataPipe);
            videoStreams.Assign(room.VideoStreams);
            audioStreams.Assign(room.AudioStreams);
            audioTracks.Assign(room.AudioTracks);

            room.RoomMetadataChanged += RoomOnRoomMetadataChanged;
            room.RoomSidChanged += RoomOnRoomSidChanged;
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
            room.Disconnected += RoomOnDisconnected;
        }

        private void Unsubscribe(IRoom previous)
        {
            previous.RoomMetadataChanged -= RoomOnRoomMetadataChanged;
            previous.RoomSidChanged -= RoomOnRoomSidChanged;
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
            previous.Disconnected -= RoomOnDisconnected;
        }

        private void RoomOnDisconnected(IRoom room, DisconnectReason disconnectReason)
        {
            Disconnected?.Invoke(room, disconnectReason);
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

        private void RoomOnRoomSidChanged(string sid)
        {
            RoomSidChanged?.Invoke(sid);
        }

        public void UpdateLocalMetadata(string metadata) =>
            assigned.UpdateLocalMetadata(metadata);

        public void SetLocalName(string name) =>
            assigned.SetLocalName(name);

        public Task<(bool success, string? errorMessage)> ConnectAsync(string url, string authToken, CancellationToken cancelToken, bool autoSubscribe) =>
            assigned.EnsureAssigned().ConnectAsync(url, authToken, cancelToken, autoSubscribe);

        public Task DisconnectAsync(CancellationToken token) =>
            assigned.EnsureAssigned().DisconnectAsync(token);
    }
}
