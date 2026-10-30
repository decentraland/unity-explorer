using DCL.Utility.Types;
using System;

namespace DCL.Profiles.Self
{
    /// <summary>Identifies one call waiting on the self-profile FSM.</summary>
    public readonly struct RequestId : IEquatable<RequestId>
    {
        public readonly long Value;

        public RequestId(long value)
        {
            Value = value;
        }

        public bool Equals(RequestId other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is RequestId other && Equals(other);

        public override int GetHashCode() =>
            Value.GetHashCode();

        public override string ToString() =>
            $"#{Value}";
    }

    /// <summary>Non-allocating inline list of up to <see cref="CAPACITY"/> waiting requests; a full list drops the oldest.</summary>
    public readonly struct RequestIds : IEquatable<RequestIds>
    {
        public const int CAPACITY = Block8<RequestId>.CAPACITY;

        private readonly Block8<RequestId> items;
        private readonly byte count;

        private RequestIds(in Block8<RequestId> items, int count)
        {
            this.items = items;
            this.count = (byte)count;
        }

        public int Count => count;

        public RequestId this[int index] =>
            index >= 0 && index < count
                ? items[index]
                : throw new ArgumentOutOfRangeException(nameof(index), index, $"The list holds {count} requests");

        public bool Contains(RequestId id) =>
            IndexOf(id) >= 0;

        /// <summary>The list with the id appended. When the list is full, the oldest id is dropped to make room.</summary>
        public RequestIds Add(RequestId id)
        {
            Block8<RequestId> next = items;
            int kept = count;

            if (kept == CAPACITY)
            {
                for (var i = 1; i < CAPACITY; i++)
                    next[i - 1] = items[i];

                kept = CAPACITY - 1;
            }

            next[kept] = id;
            return new RequestIds(next, kept + 1);
        }

        /// <summary>The same list when the id is not in it.</summary>
        public RequestIds Remove(RequestId id)
        {
            int index = IndexOf(id);

            if (index < 0)
                return this;

            Block8<RequestId> next = items;

            for (int i = index + 1; i < count; i++)
                next[i - 1] = items[i];

            next[count - 1] = default;
            return new RequestIds(next, count - 1);
        }

        public bool Equals(RequestIds other)
        {
            if (count != other.count)
                return false;

            for (var i = 0; i < count; i++)
                if (!items[i].Equals(other.items[i]))
                    return false;

            return true;
        }

        public override bool Equals(object? obj) =>
            obj is RequestIds other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            for (var i = 0; i < count; i++)
                hash.Add(items[i]);

            return hash.ToHashCode();
        }

        public override string ToString() =>
            count == 0 ? "no requests" : $"{count} requests";

        private int IndexOf(RequestId id)
        {
            for (var i = 0; i < count; i++)
                if (items[i].Equals(id))
                    return i;

            return -1;
        }
    }

    /// <summary>Non-allocating inline list of up to <see cref="CAPACITY"/> answers kept until taken; a full list drops the oldest.</summary>
    public readonly struct RequestResults<T> where T: struct
    {
        public const int CAPACITY = Block8<Entry>.CAPACITY;

        private readonly Block8<Entry> entries;
        private readonly byte count;

        private RequestResults(in Block8<Entry> entries, int count)
        {
            this.entries = entries;
            this.count = (byte)count;
        }

        public int Count => count;

        public bool Contains(RequestId id) =>
            IndexOf(id) >= 0;

        public bool TryGet(RequestId id, out T result)
        {
            int index = IndexOf(id);

            if (index < 0)
            {
                result = default;
                return false;
            }

            result = entries[index].Result;
            return true;
        }

        /// <summary>The list with the answer appended. When the list is full, the oldest answer is dropped to make room.</summary>
        public RequestResults<T> With(RequestId id, T result)
        {
            Block8<Entry> next = entries;
            int kept = count;

            if (kept == CAPACITY)
            {
                for (var i = 1; i < CAPACITY; i++)
                    next[i - 1] = entries[i];

                kept = CAPACITY - 1;
            }

            next[kept] = new Entry(id, result);
            return new RequestResults<T>(next, kept + 1);
        }

        /// <summary>The same list when the id is not in it.</summary>
        public RequestResults<T> Without(RequestId id)
        {
            int index = IndexOf(id);

            if (index < 0)
                return this;

            Block8<Entry> next = entries;

            for (int i = index + 1; i < count; i++)
                next[i - 1] = entries[i];

            next[count - 1] = default;
            return new RequestResults<T>(next, count - 1);
        }

        public override string ToString() =>
            count == 0 ? "no results" : $"{count} results";

        private int IndexOf(RequestId id)
        {
            for (var i = 0; i < count; i++)
                if (entries[i].Id.Equals(id))
                    return i;

            return -1;
        }

        private readonly struct Entry
        {
            public readonly RequestId Id;
            public readonly T Result;

            public Entry(RequestId id, T result)
            {
                Id = id;
                Result = result;
            }
        }
    }
}
