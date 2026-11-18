using System;
using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    public enum CreditPurchaseModalOutcome
    {
        Purchased,
        Dismissed,
        Failed,
    }

    public readonly struct CreditPurchaseModalControllerParams
    {
        public const string SOURCE_PASSPORT_EQUIPPED = "passport_equipped";
        public const string SOURCE_PASSPORT_CREATIONS = "passport_creations";
        public const string SOURCE_SDK_SCENE = "sdk_scene";
        public const string SOURCE_SHOP_GRID = "shop_grid";
        public const string SOURCE_SHOP_TRENDING = "shop_trending";
        public const string SOURCE_SHOP_NEW_CREATIONS = "shop_new_creations";
        public const string SOURCE_SHOP_OUTFIT = "shop_outfit";

        private const string EMOTE_CATEGORY = "emote";

        public readonly ShopListingDto Listing;
        public readonly Sprite? ItemThumbnail;
        public readonly string FallbackMarketplaceUrl;
        public readonly string Source;
        public readonly string ItemUrn;

        /// <summary>Invoked once when the modal closes, with how the purchase ended.</summary>
        public readonly Action<CreditPurchaseModalOutcome>? Closed;

        public bool IsEmote => Listing.category == EMOTE_CATEGORY;

        /// <summary>The key of the item's icon in the category icons mapping.</summary>
        public string? CategoryIconKey => IsEmote ? EMOTE_CATEGORY : Listing.wearableCategory;

        public CreditPurchaseModalControllerParams(
            ShopListingDto listing,
            Sprite? itemThumbnail,
            string fallbackMarketplaceUrl,
            string source,
            string itemUrn,
            Action<CreditPurchaseModalOutcome>? closed = null)
        {
            Listing = listing;
            ItemThumbnail = itemThumbnail;
            FallbackMarketplaceUrl = fallbackMarketplaceUrl;
            Source = source;
            ItemUrn = itemUrn;
            Closed = closed;
        }
    }
}
