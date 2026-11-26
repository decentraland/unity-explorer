using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     One way of paying a price: the credits leg in cents (and in whole credits, for display) plus the MANA
    ///     leg in wei. Either leg is zero when the method does not use it.
    /// </summary>
    public readonly struct PaymentOption
    {
        public readonly PaymentMethod Method;
        public readonly int CreditsCents;
        public readonly int Credits;
        public readonly BigInteger ManaWei;

        public PaymentOption(PaymentMethod method, int creditsCents, int credits, BigInteger manaWei)
        {
            Method = method;
            CreditsCents = creditsCents;
            Credits = credits;
            ManaWei = manaWei;
        }

        public static PaymentOption CreditsOnly(in CreditsPurchaseQuote quote) =>
            new (PaymentMethod.Credits, quote.UsdCents, quote.Credits, BigInteger.Zero);
    }
}
