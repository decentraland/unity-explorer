using System;

namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public class ThroughputBuffer : IThroughputBuffer
    {
        private ulong amount;
        private ulong amountFrame;

        public ulong CurrentAmount()
        {
            lock (this)
            {
                return amount;
            }
        }

        public ulong ConsumeFrameAmount()
        {
            lock (this)
            {
                ulong af = amountFrame;
                amountFrame = 0;
                return af;
            }
        }

        public void Register(ulong bytesAmount)
        {
            lock (this)
            {
                amount += bytesAmount;
                amountFrame += bytesAmount;
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
