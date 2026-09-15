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
        private readonly ShopCreatorNameCache creatorNames;

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
            ShopCreatorNameCache creatorNames)
        {
            this.cart = cart;
            this.buyHandler = buyHandler;
            this.api = api;
            this.webBrowser = webBrowser;
            this.urlsSource = urlsSource;
            this.identityCache = identityCache;
            this.styling = styling;
            this.thumbnailLoader = thumbnailLoader;
            this.creatorNames = creatorNames;
        }

        public void Bind(ShopItemCardView card, ShopItemCardModel model, long nowUnixSeconds)
        {
            GiftItemStyleSnapshot style = StyleOf(model);
            card.Bind(model, creatorNames.GetDisplayName(model.Creator), style, thumbnailLoader, ActionsFor(model), IsInCart(model), IsCartFull(model), nowUnixSeconds);
        }

        public void RefreshCartState(ShopItemCardView card)
        {
            if (card.Model != null)
                card.SetCartState(IsInCart(card.Model), IsCartFull(card.Model));
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

        public bool IsCartFull(ShopItemCardModel model)
        {
            if (!IsInCart(model))
                return false;

            if (model.TokenId != null)
                return true;

            string lineId = ShopListingDtoExtensions.CartLineId(model.ContractAddress, model.ItemId, model.TokenId);
            return cart.TryGet(lineId, out ShopCartLine? line) && line!.Quantity >= line.StockCap;
        }

        public async UniTask AddToCartAsync(ShopItemCardView card, ShopCartSource source, CancellationToken ct)
        {
            ShopItemCardModel? model = card.Model;

            if (model == null || card.IsResolving)
                return;

            card.SetResolving(true);
            ShopListingDto? listing = await ResolveListingAsync(model, ct);

            if (ct.IsCancellationRequested)
                return;

            card.SetResolving(false);

            if (listing == null || listing.priceCredits <= 0 || (listing.IsPrimary() && listing.available <= 0))
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ITEM_UNAVAILABLE_MESSAGE));
                return;
            }

            cart.Add(listing, source);
        }

        public async UniTask BuyAsync(ShopItemCardView card, string source, CancellationToken ct)
        {
            ShopItemCardModel? model = card.Model;

            if (model == null || card.IsResolving)
                return;

            card.SetResolving(true);
            ShopListingDto? listing = await ResolveListingAsync(model, ct);

            if (ct.IsCancellationRequested)
                return;

            card.SetResolving(false);
            string itemUrl = ItemUrl(model);

            if (listing == null)
            {
                webBrowser.OpenUrlMainThreadOnly(itemUrl);
                return;
            }

            GiftItemStyleSnapshot style = StyleOf(model);
            var visuals = new CreditPurchaseBuyHandler.ItemVisuals(model.Name, model.Rarity, card.Thumbnail.ImageSprite, style.rarityBackground, style.flapColor, style.categoryIcon);
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
