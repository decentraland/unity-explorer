using DCL.Utility.Types;
using System;

namespace DCL.PlacesAPIService
{
    /// <summary>
    ///     Domain object for a place identifier. <br />
    ///     Guaranteed to carry a non-null, non-empty value: the only way to obtain an instance is <see cref="New" />,
    ///     which rejects invalid input. Equality ignores the case of the inner value so it can be used
    ///     directly as a dictionary key without a custom comparer.
    /// </summary>
    public sealed class PlaceId : IEquatable<PlaceId>
    {
        public string Value { get; }

        private PlaceId(string value)
        {
            Value = value;
        }

        public static Option<PlaceId> New(string? raw) =>
            string.IsNullOrEmpty(raw)
                ? Option<PlaceId>.None
                : Option<PlaceId>.Some(new PlaceId(raw!));

        public bool Equals(PlaceId? other) =>
            other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) =>
            obj is PlaceId other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public override string ToString() =>
            Value;

        public static bool operator ==(PlaceId? left, PlaceId? right) =>
            left?.Equals(right) ?? right is null;

        public static bool operator !=(PlaceId? left, PlaceId? right) =>
            !(left == right);

        public static implicit operator string?(PlaceId? placeId) =>
            placeId?.Value;
    }
}
