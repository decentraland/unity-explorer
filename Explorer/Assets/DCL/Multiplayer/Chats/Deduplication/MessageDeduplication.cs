using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Chats.Deduplication
{
    public class MessageDeduplication : IMessageDeduplication
    {
        private readonly ISet<RegisteredStamp> registeredStamps = new HashSet<RegisteredStamp>();
        private readonly TimeSpan cleanPerPeriod;
        private DateTime previousClean;

        public MessageDeduplication() : this(TimeSpan.FromMinutes(5))
        {
        }

        public MessageDeduplication(TimeSpan cleanPerPeriod)
        {
            this.cleanPerPeriod = cleanPerPeriod;
            previousClean = DateTime.Now;
        }

        public bool Contains(string walletId, double timestamp) =>
            registeredStamps.Contains(new RegisteredStamp(walletId, timestamp));

        public void Register(string walletId, double timestamp)
        {
            if (DateTime.Now - previousClean > cleanPerPeriod)
            {
                previousClean = DateTime.Now;
                registeredStamps.Clear();
            }

            registeredStamps.Add(new RegisteredStamp(walletId, timestamp));
        }

        [Serializable]
        private struct RegisteredStamp
        {
            public string walletId;
            public double timestamp;

            public RegisteredStamp(string walletId, double timestamp)
            {
                this.walletId = walletId;
                this.timestamp = timestamp;
            }
        }
    }
}
