using System;

namespace DCL.MarketplaceCredits.Purchase.Cart
{
    /// <summary>
    ///     The account's favorite items as the cart sees them. The favorites live in the shop assembly, which
    ///     references this one, so the cart reaches them through this contract.
    /// </summary>
    public interface IShopCartFavorites
    {
        /// <summary>The set of favorites changed: a toggle was applied or the list was read again.</summary>
        event Action? FavoritesChanged;

        /// <summary>False for listings without an item id (per-token listings), which cannot be favorited.</summary>
        bool CanBeFavorite(ShopListingDto listing);

        bool IsFavorite(ShopListingDto listing);

        void Toggle(ShopListingDto listing, ShopCartSource source);
    }
}
