using System;

namespace DCL.Web3
{
    public readonly struct Web3Address
    {
        private readonly string address;

        public Web3Address(string address)
        {
            this.address = address;
        }

        public override string ToString() =>
            address;

        public override int GetHashCode() =>
            address.GetHashCode();

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
