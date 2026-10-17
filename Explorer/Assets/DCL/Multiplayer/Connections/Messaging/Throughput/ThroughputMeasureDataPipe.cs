using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using DCL.LiveKit.Public;

namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public class ThroughputMeasureDataPipe : IDataPipe
    {
        private readonly IDataPipe origin;
        private readonly ThroughputBuffer incomingThroughputBuffer;
        private readonly ThroughputBuffer outgoingThroughputBuffer;

        public event ReceivedDataDelegate? DataReceived;

        public ThroughputMeasureDataPipe(IDataPipe origin, ThroughputBuffer incomingThroughputBuffer, ThroughputBuffer outgoingThroughputBuffer)
        {
            this.origin = origin;
            this.incomingThroughputBuffer = incomingThroughputBuffer;
            this.outgoingThroughputBuffer = outgoingThroughputBuffer;
            this.origin.DataReceived += OriginOnDataReceived;
        }

        private void OriginOnDataReceived(ReadOnlySpan<byte> data, LKParticipant participant, string topic, LKDataPacketKind kind)
        {
            incomingThroughputBuffer.Register((ulong)data.Length);
            DataReceived?.Invoke(data, participant, topic, kind);
        }

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, LKDataPacketKind kind)
        {
            outgoingThroughputBuffer.Register((ulong)data.Length);
            origin.PublishData(data, topic, destinationSids, kind);
        }
    }
}
