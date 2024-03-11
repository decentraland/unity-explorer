using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorDataPipe : IDataPipe, IInterior<IDataPipe>
    {
        private IDataPipe assigned = NullDataPipe.INSTANCE;

        public event ReceivedDataDelegate? DataReceived;

        public void Assign(IDataPipe value, out IDataPipe? previous)
        {
            previous = assigned;
            previous.DataReceived -= OnDataReceived;
            assigned = value;
            value.DataReceived += OnDataReceived;

            previous = previous is NullDataPipe ? null : previous;
        }

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind) =>
            DataReceived?.Invoke(data, participant, kind);

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, DataPacketKind kind = DataPacketKind.KindLossy) =>
            assigned.EnsureAssigned().PublishData(data, topic, destinationSids, kind);
    }
}
