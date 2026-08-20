using DCL.Chat.MessageBus.Deduplication;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Deduplication
{
    public class MessageDeduplication<T> : IMessageDeduplication<T> where T: IComparable<T>, IEquatable<T>
    {
        /// <summary>
        ///     Retain every key registered within the period. Callers whose key is derived from
        ///     network-controlled data must pass an explicit capacity instead.
        /// </summary>
        public const int UNBOUNDED_CAPACITY = 0;

        private const double DEFAULT_CLEAN_PERIOD_MINUTES = 5;

        private readonly ISet<RegisteredStamp> registeredStamps = new HashSet<RegisteredStamp>();
        private readonly TimeSpan cleanPerPeriod;
        private readonly int capacity;
        private DateTime previousClean;

        public MessageDeduplication() : this(TimeSpan.FromMinutes(DEFAULT_CLEAN_PERIOD_MINUTES)) { }

        public MessageDeduplication(int capacity) : this(TimeSpan.FromMinutes(DEFAULT_CLEAN_PERIOD_MINUTES), capacity) { }

        public MessageDeduplication(TimeSpan cleanPerPeriod, int capacity = UNBOUNDED_CAPACITY)
        {
            this.cleanPerPeriod = cleanPerPeriod;
            this.capacity = capacity;
            previousClean = DateTime.Now;
        }

        public bool Contains(string walletId, T timestamp) =>
            registeredStamps.Contains(new RegisteredStamp(walletId, timestamp));

        public void Register(string walletId, T timestamp)
        {
            bool isFull = capacity != UNBOUNDED_CAPACITY && registeredStamps.Count >= capacity;

            // Reaching the capacity restarts the period: stamps carry no individual age, so the
            // set can only be dropped as a whole. This bounds how much a sender can make the
            // cache retain when every key it registers is distinct.
            if (isFull || DateTime.Now - previousClean > cleanPerPeriod)
            {
                previousClean = DateTime.Now;
                registeredStamps.Clear();
            }

            registeredStamps.Add(new RegisteredStamp(walletId, timestamp));
        }

        public void Remove(string walletId, T timestamp)
        {
            registeredStamps.Remove(new RegisteredStamp(walletId, timestamp));
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
