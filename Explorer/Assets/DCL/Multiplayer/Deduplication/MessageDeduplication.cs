using DCL.Chat.MessageBus.Deduplication;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Deduplication
{
    public class MessageDeduplication<T> : IMessageDeduplication<T> where T : IComparable<T>, IEquatable<T>
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

        public bool Contains(string walletId, T timestamp) =>
            registeredStamps.Contains(new RegisteredStamp(walletId, timestamp));

        public void Register(string walletId, T timestamp)
        {
            if (DateTime.Now - previousClean > cleanPerPeriod)
            {
                previousClean = DateTime.Now;
                registeredStamps.Clear();
            }

            registeredStamps.Add(new RegisteredStamp(walletId, timestamp));
        }

        [Serializable]
        internal struct RegisteredStamp : IEquatable<RegisteredStamp>
        {
            public string walletId;
            public T timestamp;

            public RegisteredStamp(string walletId, T timestamp)
            {
                this.walletId = walletId;
                this.timestamp = timestamp;
            }

            public bool Equals(RegisteredStamp other) =>
                walletId == other.walletId
                && timestamp.Equals(other.timestamp);

            public override bool Equals(object? obj) =>
                obj is RegisteredStamp other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(walletId, timestamp);
        }
    }
}
