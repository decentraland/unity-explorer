using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.UI
{
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

        public readonly string FallbackMarketplaceUrl;
        public readonly string Source;

        public CreditPurchaseModalControllerParams(
            ShopListingDto listing,
            string itemName,
            string rarityName,
            Sprite? itemThumbnail,
            Sprite? rarityBackground,
            Color rarityColor,
            Sprite? categoryIcon,
            string fallbackMarketplaceUrl,
            string source)
        {
            Listing = listing;
            ItemName = itemName;
            RarityName = rarityName;
            ItemThumbnail = itemThumbnail;
            RarityBackground = rarityBackground;
            RarityColor = rarityColor;
            CategoryIcon = categoryIcon;
            FallbackMarketplaceUrl = fallbackMarketplaceUrl;
            Source = source;
        }
    }
}
