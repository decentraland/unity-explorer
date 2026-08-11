using DCL.Utility.Types;
using DCL.Web3;
using System;

namespace DCL.Profiles
{
    /// <summary>
    ///     Domain object for a user identifier (the complete wallet address of the user). <br />
    ///     Guaranteed to carry a non-null, non-empty value: the only way to obtain an instance is <see cref="New" />,
    ///     which rejects invalid input. Equality ignores the case of the inner value so it can be used
    ///     directly as a dictionary key without a custom comparer.
    /// </summary>
    public sealed class UserId : IEquatable<UserId>
    {
        public string Value { get; }

        private UserId(string value)
        {
            Value = value;
        }

        public static Option<UserId> New(string? raw) =>
            string.IsNullOrEmpty(raw)
                ? Option<UserId>.None
                : Option<UserId>.Some(new UserId(raw!));

        /// <summary>
        ///     Carries the lowercase form of the wallet address; <see cref="Option{T}.None" /> when the address is empty
        /// </summary>
        public static Option<UserId> From(Web3Address address) =>
            New(address.ToString());

        /// <summary>
        ///     Creates a unique placeholder id for locally generated users (random avatars, fakes);
        ///     non-empty by construction, so no validation is involved
        /// </summary>
        public static UserId NewRandom() =>
            new (Guid.NewGuid().ToString());

        public bool Equals(UserId? other) =>
            other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) =>
            obj is UserId other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public override string ToString() =>
            Value;

        public static bool operator ==(UserId? left, UserId? right) =>
            left?.Equals(right) ?? right is null;

        public static bool operator !=(UserId? left, UserId? right) =>
            !(left == right);

        public static implicit operator string?(UserId? userId) =>
            userId?.Value;
    }
}
