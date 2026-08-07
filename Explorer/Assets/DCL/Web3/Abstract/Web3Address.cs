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
        /// Checks that a value is shaped like an Ethereum wallet address: "0x" followed by 40 hex digits.
        /// </summary>
        /// <param name="walletAddress">The value to check. Null and malformed values return false.</param>
        public static bool IsValidWalletAddress(string? walletAddress)
        {
            if (walletAddress == null || walletAddress.Length != ETH_ADDRESS_LENGTH) return false;

            if (walletAddress[0] != '0' || (walletAddress[1] != 'x' && walletAddress[1] != 'X')) return false;

            for (int i = 2; i < walletAddress.Length; i++)
            {
                if (walletAddress[i] is not (>= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Returns the value as a lowercased <see cref="Web3Address" /> when it is a well-formed
        ///     wallet address (see <see cref="IsValidWalletAddress" />), null otherwise. Meant for
        ///     untrusted inputs (launch arguments, deep links, query params), since the constructor
        ///     itself accepts any string and only lowercases it.
        /// </summary>
        public static Web3Address? FromUntrusted(string? address) =>
            IsValidWalletAddress(address) ? new Web3Address(address) : (Web3Address?)null;

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
