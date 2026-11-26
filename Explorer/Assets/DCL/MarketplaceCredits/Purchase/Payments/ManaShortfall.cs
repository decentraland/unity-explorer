using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     MANA the buyer holds that cannot pay for the purchase, alone or mixed, with what it is worth at the
    ///     purchase's own rate.
    /// </summary>
    public readonly struct ManaShortfall
    {
        public readonly BigInteger ManaWei;
        public readonly int ManaCents;
        public readonly int PriceCents;

        public ManaShortfall(BigInteger manaWei, int manaCents, int priceCents)
        {
            ManaWei = manaWei;
            ManaCents = manaCents;
            PriceCents = priceCents;
        }
    }
}
