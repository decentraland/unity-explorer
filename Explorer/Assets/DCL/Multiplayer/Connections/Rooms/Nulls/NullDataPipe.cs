using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullDataPipe : IDataPipe
    {
        public static readonly NullDataPipe INSTANCE = new ();

        public event ReceivedDataDelegate? DataReceived;

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, DataPacketKind kind = DataPacketKind.KindLossy)
        {
            //ignore
        }
    }
}
