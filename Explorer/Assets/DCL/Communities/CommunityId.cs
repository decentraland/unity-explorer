using DCL.Utility.Types;
using System;

namespace DCL.Communities
{
    /// <summary>
    ///     Domain object for a community identifier. <br />
    ///     Guaranteed to carry a non-null, non-empty value: the only way to obtain an instance is <see cref="New" />,
    ///     which rejects invalid input. Equality ignores the case of the inner value so it can be used
    ///     directly as a dictionary key without a custom comparer.
    /// </summary>
    public sealed class CommunityId : IEquatable<CommunityId>
    {
        public string Value { get; }

        private CommunityId(string value)
        {
            Value = value;
        }

        public static Option<CommunityId> New(string? raw) =>
            string.IsNullOrEmpty(raw)
                ? Option<CommunityId>.None
                : Option<CommunityId>.Some(new CommunityId(raw!));

        public bool Equals(CommunityId? other) =>
            other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) =>
            obj is CommunityId other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public override string ToString() =>
            Value;

        public static bool operator ==(CommunityId? left, CommunityId? right) =>
            left?.Equals(right) ?? right is null;

        public static bool operator !=(CommunityId? left, CommunityId? right) =>
            !(left == right);

        public static implicit operator string?(CommunityId? communityId) =>
            communityId?.Value;
    }
}
