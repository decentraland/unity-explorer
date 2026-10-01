using System;

namespace DCL.Profiles.Self
{
    // TODO no allocs, just buffer for 32 elements max, fixed buffers and counter, stack friendly
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

    /// <summary>Immutable list of requests waiting on the same activity. The default value is empty.</summary>
    public readonly struct RequestIds
    {
        private readonly RequestId[]? ids;

        private RequestIds(RequestId[] ids)
        {
            this.ids = ids;
        }

        public int Count => ids?.Length ?? 0;

        public RequestId this[int index] => Ids[index];

        private RequestId[] Ids => ids ?? Array.Empty<RequestId>();

        public RequestIds Add(RequestId id)
        {
            var next = new RequestId[Count + 1];
            Array.Copy(Ids, next, Count);
            next[Count] = id;
            return new RequestIds(next);
        }

        /// <summary>The same list when the id is not in it.</summary>
        public RequestIds Remove(RequestId id)
        {
            int index = Array.IndexOf(Ids, id);

            if (index < 0)
                return this;

            var next = new RequestId[Count - 1];
            Array.Copy(Ids, 0, next, 0, index);
            Array.Copy(Ids, index + 1, next, index, Count - index - 1);
            return new RequestIds(next);
        }

        public override string ToString() =>
            Count == 0 ? "no requests" : $"{Count} requests";
    }

    /// <summary>Immutable list of answered requests, kept until each requester takes its result. The default value is empty.</summary>
    public readonly struct RequestResults<T> where T: struct
    {
        private readonly (RequestId Id, T Result)[]? entries;

        private RequestResults((RequestId Id, T Result)[] entries)
        {
            this.entries = entries;
        }

        public int Count => entries?.Length ?? 0;

        private (RequestId Id, T Result)[] Entries => entries ?? Array.Empty<(RequestId Id, T Result)>();

        public bool TryGet(RequestId id, out T result)
        {
            foreach ((RequestId Id, T Result) entry in Entries)
            {
                if (!entry.Id.Equals(id))
                    continue;

                result = entry.Result;
                return true;
            }

            result = default;
            return false;
        }

        public RequestResults<T> With(RequestId id, T result)
        {
            var next = new (RequestId Id, T Result)[Count + 1];
            Array.Copy(Entries, next, Count);
            next[Count] = (id, result);
            return new RequestResults<T>(next);
        }

        /// <summary>The same list when the id is not in it.</summary>
        public RequestResults<T> Without(RequestId id)
        {
            int index = -1;

            for (var i = 0; i < Count; i++)
                if (Entries[i].Id.Equals(id))
                {
                    index = i;
                    break;
                }

            if (index < 0)
                return this;

            var next = new (RequestId Id, T Result)[Count - 1];
            Array.Copy(Entries, 0, next, 0, index);
            Array.Copy(Entries, index + 1, next, index, Count - index - 1);
            return new RequestResults<T>(next);
        }

        public override string ToString() =>
            Count == 0 ? "no results" : $"{Count} results";
    }
}
