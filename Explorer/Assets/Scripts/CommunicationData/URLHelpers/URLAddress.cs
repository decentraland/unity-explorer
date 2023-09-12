using System;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     The final URL address
    /// </summary>
    public readonly struct URLAddress : IEquatable<URLAddress>
    {
        public static readonly URLAddress EMPTY = new (string.Empty);

        public readonly string Value;

        internal URLAddress(string value)
        {
            Value = value;
        }

        public static implicit operator string(in URLAddress address) =>
            address.Value;

        public static implicit operator URLAddress(string value) =>
            new (value);

        public bool Equals(URLAddress other) =>
            Value == other.Value;

        public override bool Equals(object obj) =>
            obj is URLAddress other && Equals(other);

        public override int GetHashCode() =>
            Value.GetHashCode();

        public static bool operator ==(URLAddress left, URLAddress right) =>
            left.Equals(right);

        public static bool operator !=(URLAddress left, URLAddress right) =>
            !left.Equals(right);
    }
}
