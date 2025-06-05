using System;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     The final URL address
    /// </summary>
    public readonly struct URLAddress : IEquatable<URLAddress>
    {
        public static readonly URLAddress EMPTY = new (null!);

        public readonly Uri? Value;

        internal URLAddress(Uri? value)
        {
            Value = value;
        }

        public bool TryGetStringValue(out string value)
        {
            value = string.Empty;

            if (Value == null)
                return false;

            value = Value.OriginalString;
            return true;
        }

        public static URLAddress FromString(string value) =>
            new (new Uri(value));

        public static implicit operator Uri?(URLAddress value) =>
            value.Value;

        public override bool Equals(object obj) =>
            obj is URLAddress other && Equals(other);

        public override int GetHashCode() =>
            Value != null ? Value.GetHashCode() : 0;

        public static bool operator ==(URLAddress left, URLAddress right) =>
            left.Equals(right);

        public static bool operator !=(URLAddress left, URLAddress right) =>
            !left.Equals(right);

        public override string ToString() =>
            Value?.OriginalString ?? string.Empty;

        public bool Equals(URLAddress other) =>
            Equals(Value, other.Value);
    }
}
