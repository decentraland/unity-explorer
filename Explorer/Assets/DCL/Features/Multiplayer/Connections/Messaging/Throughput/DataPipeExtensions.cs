using DCL.Multiplayer.Connections.Systems.Throughput;
using LiveKit.Rooms.DataPipes;

namespace DCL.Multiplayer.Connections.Messaging.Throughput
{
    public static class DataPipeExtensions
    {
        public static ThroughputMeasureDataPipe WithThroughputMeasure(this IDataPipe pipe, ThroughputBufferBunch bufferBunch) =>
            new (pipe, bufferBunch.Incoming, bufferBunch.Outgoing);
    }
}
