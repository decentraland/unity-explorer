using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;
using DCL.Browser;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Passport.Modules;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Shop
{
    public class ShopItemCardPresenter
    {
        private const string ITEM_UNAVAILABLE_MESSAGE = "This item is no longer available.";

        private readonly ShopCart cart;
        private readonly CreditPurchaseBuyHandler buyHandler;
        private readonly MarketplaceShopAPIClient api;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWearableStylingCatalog styling;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly ShopCreatorCache creators;

        public bool PurchasesEnabled { get; set; }

        public event Action<ShopItemCardModel, ShopListingDto>? ListingResolved;

        public ShopItemCardPresenter(
            ShopCart cart,
            CreditPurchaseBuyHandler buyHandler,
            MarketplaceShopAPIClient api,
            UnityAppWebBrowser webBrowser,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache identityCache,
            IWearableStylingCatalog styling,
            ThumbnailLoader thumbnailLoader,
            ShopCreatorCache creators)
        {
            this.cart = cart;
            this.buyHandler = buyHandler;
            this.api = api;
            this.webBrowser = webBrowser;
            this.urlsSource = urlsSource;
            this.identityCache = identityCache;
            this.styling = styling;
            this.thumbnailLoader = thumbnailLoader;
            this.creators = creators;
        }

        public void Bind(ShopItemCardView card, ShopItemCardModel model, long nowUnixSeconds)
        {
            GiftItemStyleSnapshot style = StyleOf(model);
            card.Bind(model, creators.GetDisplayName(model.Creator), creators.GetThumbnail(model.Creator), style, thumbnailLoader, ActionsFor(model), IsInCart(model), nowUnixSeconds);
        }

        public void RefreshCartState(ShopItemCardView card)
        {
            if (card.Model != null)
                card.SetCartState(IsInCart(card.Model));
        }

        public ShopCardActions ActionsFor(ShopItemCardModel model) =>
            !PurchasesEnabled || model.IsNotForSale || IsOwnListing(model)
                ? ShopCardActions.View
                : ShopCardActions.View | ShopCardActions.AddToCart | ShopCardActions.Buy;

        public bool IsOwnListing(ShopItemCardModel model)
        {
            IWeb3Identity? identity = identityCache.Identity;
            return model.IsPrimary && identity != null && string.Equals(identity.Address, model.Creator, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsInCart(ShopItemCardModel model) =>
            model.Listing != null
                ? cart.Contains(model.Listing)
                : model.ItemId != null && cart.Contains(model.ContractAddress, model.ItemId);

        private static string CartLineIdOf(ShopItemCardModel model) =>
            model.Listing != null
                ? model.Listing.CartLineId()
                : ShopListingDtoExtensions.CartLineId(model.ContractAddress, model.ItemId, null);

        public bool IsCartFull(ShopItemCardModel model)
        {
            if (!IsInCart(model))
                return false;

            if (model.TokenId != null)
                return true;

            string lineId = ShopListingDtoExtensions.CartLineId(model.ContractAddress, model.ItemId, model.TokenId);
            return cart.TryGet(lineId, out ShopCartLine? line) && line!.Quantity >= line.StockCap;
        }

        /// <summary>Adds the card's item to the cart, or removes its whole line when it is already there.</summary>
        public UniTask ToggleCartAsync(ShopItemCardView card, ShopCartSource source, CancellationToken ct)
        {
            if (card.Model == null || card.IsResolving)
                return UniTask.CompletedTask;

            if (!IsInCart(card.Model))
                return AddToCartAsync(card.Model, source, card.SetResolving, ct);

            cart.Remove(CartLineIdOf(card.Model));
            return UniTask.CompletedTask;
        }

        public UniTask BuyAsync(ShopItemCardView card, string source, CancellationToken ct) =>
            card.Model == null || card.IsResolving
                ? UniTask.CompletedTask
                : BuyAsync(card.Model, card.Thumbnail.ImageSprite, source, card.SetResolving, ct);

        public async UniTask AddToCartAsync(ShopItemCardModel model, ShopCartSource source, Action<bool> setResolving, CancellationToken ct)
        {
            setResolving(true);
            ShopListingDto? listing = await ResolveListingAsync(model, ct);

            if (ct.IsCancellationRequested)
                return;

            setResolving(false);

            if (listing == null || listing.priceCredits <= 0 || (listing.IsPrimary() && listing.available <= 0))
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ITEM_UNAVAILABLE_MESSAGE));
                return;
            }

            cart.Add(listing, source, creatorName: creators.GetDisplayName(model.Creator));
        }

        public async UniTask BuyAsync(ShopItemCardModel model, Sprite? thumbnail, string source, Action<bool> setResolving, CancellationToken ct)
        {
            setResolving(true);
            ShopListingDto? listing = await ResolveListingAsync(model, ct);

            if (ct.IsCancellationRequested)
                return;

            setResolving(false);
            string itemUrl = ItemUrl(model);

            if (listing == null)
            {
                webBrowser.OpenUrlMainThreadOnly(itemUrl);
                return;
            }

            GiftItemStyleSnapshot style = StyleOf(model);
            var visuals = new CreditPurchaseBuyHandler.ItemVisuals(model.Name, model.Rarity, thumbnail, style.rarityBackground, style.flapColor, style.categoryIcon);
            await buyHandler.HandleBuyClickAsync(listing, model.Urn, itemUrl, visuals, source, ct);
        }

        public void View(ShopItemCardModel model) =>
            webBrowser.OpenUrlMainThreadOnly(ItemUrl(model));

        private string ItemUrl(ShopItemCardModel model) =>
            ShopItemLinks.BuildItemUrl(urlsSource, model.ContractAddress, model.ItemId ?? string.Empty);

        private GiftItemStyleSnapshot StyleOf(ShopItemCardModel model) =>
            styling.GetStyleSnapshot(model.Rarity, model.IsEmote ? ShopItemCardModel.CATEGORY_EMOTE : model.WearableCategory);

        private async UniTask<ShopListingDto?> ResolveListingAsync(ShopItemCardModel model, CancellationToken ct)
        {
            if (model.Listing != null)
                return model.Listing;

            if (model.ItemId == null)
                return null;

            Result<ShopListingDto?> result = await api.GetShopListingForItemAsync(model.ContractAddress, model.ItemId, false, ct).SuppressToResultAsync(ReportCategory.UI);

            if (!result.Success || result.Value == null)
                return null;

            ListingResolved?.Invoke(model, result.Value);
            return result.Value;
        }
    }
}
