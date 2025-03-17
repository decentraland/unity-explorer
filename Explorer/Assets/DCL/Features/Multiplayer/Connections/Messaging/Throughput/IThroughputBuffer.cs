namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public interface IThroughputBuffer
    {
        ulong CurrentAmount();

        void Register(ulong bytesAmount);

        void Clear();
    }
}
