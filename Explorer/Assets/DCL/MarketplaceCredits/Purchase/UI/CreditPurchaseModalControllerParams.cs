using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    public readonly struct CreditPurchaseModalControllerParams
    {
        public readonly ShopListingDto Listing;
        public readonly string ItemName;
        public readonly string RarityName;

        public readonly Sprite? ItemThumbnail;
        public readonly Sprite? RarityBackground;
        public readonly Color RarityColor;
        public readonly Sprite? CategoryIcon;

        public readonly string FallbackMarketplaceUrl;

        public CreditPurchaseModalControllerParams(
            ShopListingDto listing,
            string itemName,
            string rarityName,
            Sprite? itemThumbnail,
            Sprite? rarityBackground,
            Color rarityColor,
            Sprite? categoryIcon,
            string fallbackMarketplaceUrl)
        {
            Listing = listing;
            ItemName = itemName;
            RarityName = rarityName;
            ItemThumbnail = itemThumbnail;
            RarityBackground = rarityBackground;
            RarityColor = rarityColor;
            CategoryIcon = categoryIcon;
            FallbackMarketplaceUrl = fallbackMarketplaceUrl;
        }
    }
}
