namespace DCL.MarketplaceCredits.Purchase.Cart
{
    public enum ShopCartSource
    {
        Grid,
        Trending,
        NewCreations,
        Outfit,
    }

    public static class ShopCartSources
    {
        /// <summary>The web shop's AddToCartSource vocabulary, so both clients land in one funnel.</summary>
        public static string ToWire(ShopCartSource source) =>
            source switch
            {
                ShopCartSource.Trending => "trending",
                ShopCartSource.NewCreations => "new_creations",
                ShopCartSource.Outfit => "outfit",
                _ => "grid",
            };
    }

    /// <summary>
    ///     One cart row: the listing snapshot from the moment it was added (first touch wins) and how many units the
    ///     buyer wants. A secondary listing is one unique token, so its quantity is locked at 1.
    /// </summary>
    public sealed class ShopCartLine
    {
        public readonly string Id;
        public readonly ShopListingDto Listing;
        public readonly ShopCartSource Source;
        public readonly string? OutfitId;

        public int Quantity { get; internal set; }

        public bool IsPrimary => Listing.IsPrimary();

        /// <summary>How many units may be added: the remaining supply of a mint, 1 for a token, unbounded when unknown.</summary>
        public int StockCap
        {
            get
            {
                if (!IsPrimary)
                    return 1;

                return Listing.available > 0 ? Listing.available : int.MaxValue;
            }
        }

        public int TotalCredits => Listing.priceCredits * Quantity;

        internal ShopCartLine(ShopListingDto listing, ShopCartSource source, string? outfitId)
        {
            Id = listing.CartLineId();
            Listing = listing;
            Source = source;
            OutfitId = outfitId;
            Quantity = 1;
        }
    }
}
