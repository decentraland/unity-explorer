﻿using System;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     The final URL address
    /// </summary>
    public readonly struct URLAddress : IEquatable<URLAddress>, IEquatable<string>
    {
        public static readonly URLAddress EMPTY = new (string.Empty);

        public readonly string Value;

        internal URLAddress(string value)
        {
            Value = value;
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
    }
}
