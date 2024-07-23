using System;
using System.Text.RegularExpressions;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     The final URL address
    /// </summary>
    public readonly struct URLAddress : IEquatable<URLAddress>, IEquatable<string>
    {
        public static readonly URLAddress EMPTY = new (string.Empty);

        public readonly string Value;
        private readonly string CacheableURL;

        private static readonly string HTTP_STARTER = "https";
        private static readonly string VALIDATION_PATTERN = "/v[0-9]+/";

        internal URLAddress(string value)
        {
            Value = value;

            if (!string.IsNullOrEmpty(Value) && Value.StartsWith(HTTP_STARTER))
                CacheableURL = Regex.Replace(Value, VALIDATION_PATTERN, "/");
            else
                CacheableURL = Value;
        }

        public static implicit operator string(in URLAddress address) =>
            address.Value;

        public static URLAddress FromString(string value) =>
            new (value);

        //public static implicit operator URLAddress(string value) =>
        //    new (value);

        public bool Equals(URLAddress other) =>
            Value == other.Value;

        public bool Equals(string other) =>
            Value == other;

        public override bool Equals(object obj) =>
            obj is URLAddress other && Equals(other);

        public override int GetHashCode() =>
            Value != null ? Value.GetHashCode() : 0;

        public static bool operator ==(URLAddress left, URLAddress right) =>
            left.Equals(right);

        public static bool operator !=(URLAddress left, URLAddress right) =>
            !left.Equals(right);

        public override string ToString() =>
            Value;

        public string GetCacheableURL()
        {
            return CacheableURL;
        }
    }
}
