using System;

namespace Utility.Times
{
    public readonly struct UnixTimeMs : IEquatable<UnixTimeMs>
    {
        public readonly long Value;

        public UnixTimeMs(long value)
        {
            Value = value;
        }

        public static UnixTimeMs Now() =>
            new (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        public static long operator -(UnixTimeMs left, UnixTimeMs right) =>
            left.Value - right.Value;

        public static UnixTimeMs operator +(UnixTimeMs time, long milliseconds) =>
            new (time.Value + milliseconds);

        public static UnixTimeMs operator -(UnixTimeMs time, long milliseconds) =>
            new (time.Value - milliseconds);

        public static bool operator ==(UnixTimeMs left, UnixTimeMs right) =>
            left.Value == right.Value;

        public static bool operator !=(UnixTimeMs left, UnixTimeMs right) =>
            left.Value != right.Value;

        public bool Equals(UnixTimeMs other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is UnixTimeMs other && Equals(other);

        public override int GetHashCode() =>
            Value.GetHashCode();

        public override string ToString() =>
            DateTimeOffset.FromUnixTimeMilliseconds(Value).ToString("O");
    }
}
