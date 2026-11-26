using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>The MANA one contract will pull from the buyer in a payment.</summary>
    public readonly struct ManaSpend
    {
        public readonly string Spender;
        public readonly BigInteger Wei;

        public ManaSpend(string spender, BigInteger wei)
        {
            Spender = spender;
            Wei = wei;
        }
    }
}
