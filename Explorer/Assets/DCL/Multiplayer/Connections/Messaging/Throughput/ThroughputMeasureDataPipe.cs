using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public class ThroughputMeasureDataPipe : IDataPipe
    {
        private readonly IDataPipe origin;
        private readonly IThroughputBuffer incomingThroughputBuffer;
        private readonly IThroughputBuffer outgoingThroughputBuffer;

        public event ReceivedDataDelegate? DataReceived;

        public ThroughputMeasureDataPipe(IDataPipe origin, IThroughputBuffer incomingThroughputBuffer, IThroughputBuffer outgoingThroughputBuffer)
        {
            this.origin = origin;
            this.incomingThroughputBuffer = incomingThroughputBuffer;
            this.outgoingThroughputBuffer = outgoingThroughputBuffer;
            this.origin.DataReceived += OriginOnDataReceived;
        }

        private void OriginOnDataReceived(ReadOnlySpan<byte> data, Participant participant, string topic, DataPacketKind kind)
        {
            incomingThroughputBuffer.Register((ulong)data.Length);
            DataReceived?.Invoke(data, participant, topic, kind);
        }

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, DataPacketKind kind)
        {
            outgoingThroughputBuffer.Register((ulong)data.Length);
            origin.PublishData(data, topic, destinationSids, kind);
        }
    }
}
