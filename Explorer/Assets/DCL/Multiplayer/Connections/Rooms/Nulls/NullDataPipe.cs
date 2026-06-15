using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using System;
using System.Collections.Generic;
using DCL.LiveKit.Public;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullDataPipe : IDataPipe
    {
#pragma warning disable CS0067 // NullDataPipe is a no-op IDataPipe null object; this interface event is intentionally never raised
        public static readonly NullDataPipe INSTANCE = new ();

        public event ReceivedDataDelegate? DataReceived;

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, LKDataPacketKind kind = LKDataPacketKind.KindLossy)
        {
            //ignore
        }
#pragma warning restore CS0067
    }
}
