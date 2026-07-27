using DCL.Web3.Abstract;
using System;
using Unity.Burst;

namespace DCL.Web3
{
    public readonly struct Web3Address : IEquatable<Web3Address>
    {
        // ETH wallet address constants
        public const int ETH_ADDRESS_LENGTH = 42; // "0x" + 40 hex characters

        public readonly string OriginalFormat;
        private readonly string address;

        public Web3Address(IWeb3Account web3Account) : this(web3Account.Address.address) {
        }

        public Web3Address(string? address)
        {
            address ??= string.Empty;
            OriginalFormat = address;
            this.address = address.ToLower();
        }

        /// <summary>
        ///     Whether the value is a strictly valid Ethereum address ("0x" + 40 hex chars).
        ///     Meant for untrusted inputs (launch arguments, deep links, query params) before
        ///     wrapping them in a <see cref="Web3Address" /> — the constructor itself accepts
        ///     any string and only lowercases it.
        /// </summary>
        public static bool IsValid(string? address)
        {
            if (address == null || address.Length != ETH_ADDRESS_LENGTH)
                return false;

            if (address[0] != '0' || address[1] != 'x')
                return false;

            for (var i = 2; i < address.Length; i++)
            {
                char c = address[i];
                bool isHexDigit = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

                if (!isHexDigit)
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Returns the value as a lowercased <see cref="Web3Address" /> when it is strictly
        ///     valid (see <see cref="IsValid" />), null otherwise.
        /// </summary>
        public static Web3Address? FromUntrusted(string? address) =>
            IsValid(address) ? new Web3Address(address) : (Web3Address?)null;

        public override string ToString() =>
            address;

        public override int GetHashCode() =>
            address.GetHashCode();

        [BurstDiscard]
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;

            return obj switch
                   {
                       string s => Equals(s),
                       Web3Address a => Equals(a),
                       _ => false,
                   };
        }

        public bool Equals(string? s)
        {
            if (s == null) return false;
            return address.Equals(s, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(Web3Address a) =>
            Equals(a.address);

        public static bool operator ==(Web3Address x, string? y) =>
            x.Equals(y);

        public static bool operator !=(Web3Address x, string? y) =>
            !x.Equals(y);

        public static bool operator ==(string? y, Web3Address x) =>
            x.Equals(y);

        public static bool operator !=(string? y, Web3Address x) =>
            !x.Equals(y);

        public static implicit operator string(Web3Address source) =>
            source.ToString();
    }
}
