using System;

namespace DCL.MarketplaceCredits.Purchase
{
    public static class ShopListingDtoExtensions
    {
        public const string ACQUISITION_STORE = "store";
        public const string LISTING_TYPE_PRIMARY = "primary";
        public const string CATEGORY_EMOTE = "emote";

        public static bool IsStoreMint(this ShopListingDto listing) =>
            string.Equals(listing.acquisition, ACQUISITION_STORE, StringComparison.OrdinalIgnoreCase);

        public static bool IsPrimary(this ShopListingDto listing) =>
            string.IsNullOrEmpty(listing.tokenId);

        public static bool IsEmote(this ShopListingDto listing) =>
            string.Equals(listing.category, CATEGORY_EMOTE, StringComparison.OrdinalIgnoreCase);

        public static string CartLineId(this ShopListingDto listing) =>
            CartLineId(listing.contractAddress, listing.itemId, listing.tokenId);

        public static string CartLineId(string contractAddress, string? itemId, string? tokenId) =>
            string.IsNullOrEmpty(tokenId)
                ? $"{contractAddress.ToLowerInvariant()}-{itemId}"
                : $"{contractAddress.ToLowerInvariant()}-t{tokenId}";
    }
}
