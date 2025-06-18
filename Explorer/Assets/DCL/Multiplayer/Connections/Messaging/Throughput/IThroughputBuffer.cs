namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public interface IThroughputBuffer
    {
        ulong CurrentAmount();

        ulong CurrentAmountFrame();

        void Register(ulong bytesAmount);

        void Clear();
    }
}
