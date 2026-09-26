namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    public readonly struct CreditPack
    {
        public readonly string Id;
        public readonly float PriceUsd;
        public readonly int Credits;
        public readonly bool BestValue;
        public readonly string ImageUrl;

        public CreditPack(string id, float priceUsd, int credits, bool bestValue, string imageUrl)
        {
            Id = id;
            PriceUsd = priceUsd;
            Credits = credits;
            BestValue = bestValue;
            ImageUrl = imageUrl;
        }
    }
}
