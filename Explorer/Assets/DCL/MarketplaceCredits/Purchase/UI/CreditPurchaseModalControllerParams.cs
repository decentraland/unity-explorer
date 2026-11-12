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

        public readonly ShopListingDto Listing;
        public readonly string ItemName;
        public readonly string RarityName;

        public readonly Sprite? ItemThumbnail;
        public readonly Sprite? RarityBackground;
        public readonly Color RarityColor;
        public readonly Sprite? CategoryIcon;

        /// <summary>
        ///     True when the rarity background, rarity colour and category icon were not supplied and the modal
        ///     derives them from <see cref="Listing" />.
        /// </summary>
        public readonly bool VisualsFromListing;

        public readonly string FallbackMarketplaceUrl;
        public readonly string Source;
        public readonly string ItemUrn;

        /// <summary>Invoked once when the modal closes, with how the purchase ended.</summary>
        public readonly Action<CreditPurchaseModalOutcome>? Closed;

        public bool IsEmote => Listing.category == "emote";

        public CreditPurchaseModalControllerParams(
            ShopListingDto listing,
            string itemName,
            string rarityName,
            Sprite? itemThumbnail,
            Sprite? rarityBackground,
            Color rarityColor,
            Sprite? categoryIcon,
            string fallbackMarketplaceUrl,
            string source,
            string itemUrn)
        {
            Listing = listing;
            ItemName = itemName;
            RarityName = rarityName;
            ItemThumbnail = itemThumbnail;
            RarityBackground = rarityBackground;
            RarityColor = rarityColor;
            CategoryIcon = categoryIcon;
            VisualsFromListing = false;
            FallbackMarketplaceUrl = fallbackMarketplaceUrl;
            Source = source;
            ItemUrn = itemUrn;
            Closed = null;
        }

        /// <summary>
        ///     For a caller that knows the item only through its listing: name and rarity come from the listing,
        ///     and the modal resolves the rarity and category visuals.
        /// </summary>
        public CreditPurchaseModalControllerParams(
            ShopListingDto listing,
            Sprite? itemThumbnail,
            string fallbackMarketplaceUrl,
            string source,
            string itemUrn,
            Action<CreditPurchaseModalOutcome> closed)
        {
            Listing = listing;
            ItemName = listing.name;
            RarityName = listing.rarity;
            ItemThumbnail = itemThumbnail;
            RarityBackground = null;
            RarityColor = default(Color);
            CategoryIcon = null;
            VisualsFromListing = true;
            FallbackMarketplaceUrl = fallbackMarketplaceUrl;
            Source = source;
            ItemUrn = itemUrn;
            Closed = closed;
        }
    }
}
