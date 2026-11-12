using DCL.MarketplaceCredits.Purchase;
using System.Numerics;

namespace DCL.Shop
{
    /// <summary>
    ///     What a card of the resale names grid shows: a NAME listed by its owner and its price in MANA.
    /// </summary>
    public sealed class ShopNameCardModel
    {
        private const long PRICE_ABBREVIATION_START = 1_000;
        private const int HUNDREDTHS_PER_MANA = 100;

        private static readonly BigInteger WEI_PER_MANA = BigInteger.Pow(10, 18);
        private static readonly BigInteger WEI_PER_HUNDREDTH = BigInteger.Pow(10, 16);

        public readonly string Key;
        public readonly string Name;
        public readonly string PriceText;

        /// <summary>
        ///     The path of the NAME's page, relative to the marketplace site.
        /// </summary>
        public readonly string MarketplacePath;

        private ShopNameCardModel(string key, string name, string priceText, string marketplacePath)
        {
            Key = key;
            Name = name;
            PriceText = priceText;
            MarketplacePath = marketplacePath;
        }

        /// <summary>
        ///     Null for a row that carries no order or a price that is not a wei amount.
        /// </summary>
        public static ShopNameCardModel? FromListing(NameListingDto listing)
        {
            if (listing.order == null || !BigInteger.TryParse(listing.order.price, out BigInteger priceWei) || priceWei.Sign < 0)
                return null;

            return new ShopNameCardModel(listing.nft.id, listing.nft.name, FormatManaPrice(priceWei), listing.nft.url);
        }

        /// <summary>
        ///     Whole MANA abbreviated like the credits prices from a thousand up (1.5k), and up to two decimals below it (12.25).
        /// </summary>
        public static string FormatManaPrice(BigInteger priceWei)
        {
            BigInteger whole = BigInteger.DivRem(priceWei, WEI_PER_MANA, out BigInteger remainderWei);

            if (whole >= PRICE_ABBREVIATION_START)
                return ShopItemCardModel.FormatPrice(whole > long.MaxValue ? long.MaxValue : (long)whole);

            var hundredths = (int)(remainderWei / WEI_PER_HUNDREDTH);

            if (hundredths == 0)
                return whole.ToString();

            return hundredths % 10 == 0
                ? $"{whole}.{hundredths / 10}"
                : $"{whole}.{hundredths:00}";
        }
    }
}
