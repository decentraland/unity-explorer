using System;

namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public class ThroughputBuffer : IThroughputBuffer
    {
        private ulong amount;
        public ulong CurrentAmount()
        {
            lock (this)
            {
                return amount;
            }
        }

        public void Register(ulong bytesAmount)
        {
            lock (this)
            {
                amount += bytesAmount;
            }
        }

        public void Clear()
        {
            lock (this)
            {
                amount = 0;
            }
        }
    }
}
