using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    public readonly struct ShopCartModalParams
    {
        public const string SOURCE_SHOP_HEADER = "shop_header";
        public const string SOURCE_ADD_TO_CART = "add_to_cart";

        public readonly string Source;

        /// <summary>World-space bottom-right corner of the control that opened the cart; the popup hangs under it.</summary>
        public readonly Vector3 AnchorBottomRight;

        public readonly IShopCartFavorites Favorites;

        public ShopCartModalParams(string source, Vector3 anchorBottomRight, IShopCartFavorites favorites)
        {
            Source = source;
            AnchorBottomRight = anchorBottomRight;
            Favorites = favorites;
        }
    }
}
