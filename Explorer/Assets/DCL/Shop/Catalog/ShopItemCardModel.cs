using DCL.MarketplaceCredits.Purchase;
using System;

namespace DCL.Shop
{
    /// <summary>
    ///     What a shop card shows, built from either feed: a unified listing row (on sale, cart-ready) or a
    ///     catalogue row (all / not for sale, resolved to a listing only when the buyer acts on it).
    /// </summary>
    public sealed class ShopItemCardModel
    {
        public const string CATEGORY_WEARABLE = "wearable";
        public const string CATEGORY_EMOTE = "emote";
        private const string LISTING_TYPE_PRIMARY = "primary";

        public readonly string Key;
        public readonly string ContractAddress;
        public readonly string? ItemId;
        public readonly string? TokenId;
        public readonly string Name;
        public readonly string Creator;
        public readonly string ThumbnailUrl;
        public readonly string Rarity;
        public readonly string Category;
        public readonly string? WearableCategory;
        public readonly int PriceCredits;
        public readonly int? Available;
        public readonly bool IsSmart;
        public readonly string? Gender;
        public readonly int? CompareAtCredits;
        public readonly long? SaleEndsAtUnixSeconds;
        public readonly int ChainId;
        public readonly string Urn;
        public readonly bool HasCreatorMint;
        public readonly ShopListingDto? Listing;

        public bool IsPrimary => TokenId == null;
        public bool IsEmote => Category == CATEGORY_EMOTE;
        public bool IsNotForSale => PriceCredits <= 0 || Available == 0;

        private ShopItemCardModel(string key, string contractAddress, string? itemId, string? tokenId, string name, string creator, string thumbnailUrl,
            string rarity, string category, string? wearableCategory, int priceCredits, int? available, bool isSmart, string? gender,
            int? compareAtCredits, long? saleEndsAtUnixSeconds, int chainId, string urn, bool hasCreatorMint, ShopListingDto? listing)
        {
            Key = key;
            ContractAddress = contractAddress;
            ItemId = itemId;
            TokenId = tokenId;
            Name = name;
            Creator = creator;
            ThumbnailUrl = thumbnailUrl;
            Rarity = rarity;
            Category = category;
            WearableCategory = wearableCategory;
            PriceCredits = priceCredits;
            Available = available;
            IsSmart = isSmart;
            Gender = gender;
            CompareAtCredits = compareAtCredits;
            SaleEndsAtUnixSeconds = saleEndsAtUnixSeconds;
            ChainId = chainId;
            Urn = urn;
            HasCreatorMint = hasCreatorMint;
            Listing = listing;
        }

        public static ShopItemCardModel FromListing(ShopListingDto dto)
        {
            string contract = dto.contractAddress.ToLowerInvariant();
            string? tokenId = string.IsNullOrEmpty(dto.tokenId) ? null : dto.tokenId;
            string lineId = dto.CartLineId();

            return new ShopItemCardModel(
                string.IsNullOrEmpty(dto.tradeId) ? lineId : dto.tradeId,
                contract,
                dto.itemId,
                tokenId,
                dto.name,
                dto.creator.ToLowerInvariant(),
                dto.thumbnail,
                dto.rarity,
                dto.category,
                dto.wearableCategory,
                dto.priceCredits,
                tokenId == null ? dto.available : null,
                dto.isSmart == true,
                dto.gender,
                dto.compareAtCredits,
                dto.saleEndsAt,
                dto.chainId,
                ShopItemLinks.BuildItemUrn(dto.chainId, contract, dto.itemId ?? string.Empty),
                string.Equals(dto.listingType, LISTING_TYPE_PRIMARY, StringComparison.OrdinalIgnoreCase),
                dto);
        }

        public static ShopItemCardModel FromCatalogItem(CatalogItemDto dto)
        {
            string contract = dto.contractAddress.ToLowerInvariant();
            string lineId = ShopListingDtoExtensions.CartLineId(contract, dto.itemId, null);

            return new ShopItemCardModel(
                string.IsNullOrEmpty(dto.tradeId) ? lineId : dto.tradeId,
                contract,
                dto.itemId,
                null,
                dto.name,
                (dto.creator ?? string.Empty).ToLowerInvariant(),
                dto.thumbnail ?? string.Empty,
                dto.rarity ?? string.Empty,
                dto.category,
                dto.WearableCategory(),
                dto.priceCredits ?? 0,
                dto.available,
                dto.IsSmart(),
                dto.Gender(),
                null,
                null,
                dto.chainId,
                string.IsNullOrEmpty(dto.urn) ? ShopItemLinks.BuildItemUrn(dto.chainId, contract, dto.itemId ?? string.Empty) : dto.urn,
                !string.IsNullOrEmpty(dto.price) && dto.price != "0",
                null);
        }

        public bool IsSaleActive(long nowUnixSeconds) =>
            CompareAtCredits.HasValue && CompareAtCredits.Value > PriceCredits
            && (SaleEndsAtUnixSeconds == null || SaleEndsAtUnixSeconds.Value > nowUnixSeconds);

        public int DiscountPercent()
        {
            if (!CompareAtCredits.HasValue || CompareAtCredits.Value <= PriceCredits || CompareAtCredits.Value <= 0)
                return 0;

            int percent = (int)Math.Round((1d - (PriceCredits / (double)CompareAtCredits.Value)) * 100d);
            return Math.Clamp(percent, 1, 99);
        }

        public static string FormatCountdown(long secondsLeft)
        {
            if (secondsLeft <= 0)
                return string.Empty;

            long days = secondsLeft / 86400;
            long hours = secondsLeft % 86400 / 3600;
            long minutes = secondsLeft % 3600 / 60;
            long seconds = secondsLeft % 60;

            if (days > 0)
                return $"{days}d {hours}h";

            if (hours > 0)
                return $"{hours}h {minutes}m";

            return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
        }
    }
}
