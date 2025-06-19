namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public interface IThroughputBuffer
    {
        ulong CurrentAmount();

        ulong ConsumeFrameAmount();

        void Register(ulong bytesAmount);

        void Clear();
    }
}
