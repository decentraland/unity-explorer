namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    public readonly struct CreditPack
    {
        public readonly string Id;
        public readonly int PriceUsd;
        public readonly int Credits;
        public readonly bool BestValue;

        public CreditPack(string id, int priceUsd, int credits, bool bestValue)
        {
            Id = id;
            PriceUsd = priceUsd;
            Credits = credits;
            BestValue = bestValue;
        }
    }

    public static class CreditPackCatalog
    {
        public static readonly CreditPack[] PACKS =
        {
            new ("pack_5", 5, 50, false),
            new ("pack_10", 10, 100, false),
            new ("pack_25", 25, 250, true),
            new ("pack_50", 50, 500, false),
        };
    }
}
