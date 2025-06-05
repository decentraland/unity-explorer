using System;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     Protocol + full domain. The base part of the URL. Can be extended with subdirectories and paths.
    ///     <para>
    ///         Indicates path strictly without parameters and port
    ///     </para>
    ///     <para>
    ///         <b>https://blog.hubspot.com</b> in https://blog.hubspot.com/marketing/parts-url
    ///     </para>
    ///     or
    ///     <para>
    ///         <b>https://blog.hubspot.com/marketing</b> in https://blog.hubspot.com/marketing/parts-url
    ///     </para>
    /// </summary>
    public readonly struct URLDomain : IEquatable<URLDomain>
    {
        public static readonly URLDomain EMPTY = new (string.Empty);
        public readonly string Value;

        internal URLDomain(string value)
        {
            Value = value;
        }

        public bool IsEmpty => Value == string.Empty;

        public static URLDomain FromString(string url) =>
            new (url);

        public static URLDomain FromString(Uri url) =>
            new (url.OriginalString);

        public bool Equals(URLDomain other) =>
            Value == other.Value;

        public override bool Equals(object obj) =>
            obj is URLDomain other && Equals(other);

        public override int GetHashCode() =>
            Value != null ? Value.GetHashCode() : 0;

        public static bool operator ==(URLDomain left, URLDomain right) =>
            left.Equals(right);

        public static bool operator !=(URLDomain left, URLDomain right) =>
            !left.Equals(right);

        public override string ToString() =>
            Value;
    }
}
