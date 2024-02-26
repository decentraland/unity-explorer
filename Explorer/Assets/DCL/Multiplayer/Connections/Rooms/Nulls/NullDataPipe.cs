using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullDataPipe : IDataPipe
    {
        public static readonly NullDataPipe INSTANCE = new ();

        public void PublishData(Span<byte> data, string topic, IReadOnlyList<string> destinationSids, DataPacketKind kind = DataPacketKind.KindLossy)
        {
            //ignore
        }

        public event ReceivedDataDelegate? DataReceived;
    }
}
