using DCL.Multiplayer.Connections.Archipelago.Rooms;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Tracks.Hub;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Movement.ECS.System
{
    public class IslandRoomMock : IArchipelagoIslandRoom, IRoom, IDataPipe
    {
        public IActiveSpeakers ActiveSpeakers { get; }

        public IParticipantsHub Participants { get; }

        public IDataPipe DataPipe => this;

        public event ReceivedDataDelegate? DataReceived;

        public IRoom Room() =>
            this;

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, DataPacketKind kind = DataPacketKind.KindLossy)
        {
            DataReceived?.Invoke(data, new Participant(), kind);
        }

#region Empty Implementations
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

        public void Start() { }

        public void Stop() { }

        public bool IsRunning() =>
            true;

        public Task<bool> Connect(string url, string authToken, CancellationToken cancelToken) =>
            Task.FromResult(true);

        public void Disconnect() { }
#endregion
    }
}
