using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.Gifting.Styling;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Shop
{
    /// <summary>
    ///     The right-side panel of an open shop item: the card data at once, then the description and the hidden
    ///     categories read from the item's entity.
    /// </summary>
    public class ShopItemPanelController : IDisposable
    {
        private readonly ShopItemPanelView view;
        private readonly ShopItemCardPresenter presenter;
        private readonly ShopCreatorCache creators;
        private readonly MarketplaceShopAPIClient api;
        private readonly IWearableStylingCatalog styling;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEmoteProvider emoteProvider;
        private readonly ShopFavorites favorites;
        private readonly HashSet<string> hidingList = new ();
        private readonly List<URN> pointers = new (1);
        private readonly List<string> catalogIds = new (1);
        private readonly List<string> creatorScratch = new (1);
        private readonly List<IWearable> wearablesBuffer = new (2);
        private readonly List<IEmote> emotesBuffer = new (1);

        private CancellationTokenSource? detailsCts;
        private CancellationTokenSource? actionsCts;
        private CancellationTokenSource? creatorCts;
        private ShopCartSource cartSource;
        private string buySource = string.Empty;

        public ShopItemCardModel? Model { get; private set; }

        public event Action? CloseClicked;
        public event Action<IWearable>? WearableResolved;
        public event Action? PreviewUnavailable;

        public ShopItemPanelController(
            ShopItemPanelView view,
            ShopItemCardPresenter presenter,
            ShopCreatorCache creators,
            MarketplaceShopAPIClient api,
            IWearableStylingCatalog styling,
            NftTypeIconSO rarityBackgrounds,
            ThumbnailLoader thumbnailLoader,
            IWearablesProvider wearablesProvider,
            IEmoteProvider emoteProvider,
            ShopFavorites favorites)
        {
            this.view = view;
            this.presenter = presenter;
            this.creators = creators;
            this.api = api;
            this.styling = styling;
            this.rarityBackgrounds = rarityBackgrounds;
            this.thumbnailLoader = thumbnailLoader;
            this.wearablesProvider = wearablesProvider;
            this.emoteProvider = emoteProvider;
            this.favorites = favorites;

            view.CloseClicked += OnCloseClicked;
            view.AddToCartClicked += OnAddToCartClicked;
            view.BuyClicked += OnBuyClicked;
            view.MarketplaceClicked += OnMarketplaceClicked;
            view.FavoriteClicked += OnFavoriteClicked;
            favorites.Changed += OnFavoriteChanged;
            favorites.Loaded += OnFavoritesLoaded;
        }

        public void Dispose()
        {
            view.CloseClicked -= OnCloseClicked;
            view.AddToCartClicked -= OnAddToCartClicked;
            view.BuyClicked -= OnBuyClicked;
            view.MarketplaceClicked -= OnMarketplaceClicked;
            view.FavoriteClicked -= OnFavoriteClicked;
            favorites.Changed -= OnFavoriteChanged;
            favorites.Loaded -= OnFavoritesLoaded;
            detailsCts.SafeCancelAndDispose();
            actionsCts.SafeCancelAndDispose();
            creatorCts.SafeCancelAndDispose();
        }

        public void Show(ShopItemCardModel model, ShopCartSource source, string buySourceName, BodyShape bodyShape)
        {
            Model = model;
            cartSource = source;
            buySource = buySourceName;
            actionsCts = actionsCts.SafeRestart();

            view.gameObject.SetActive(true);
            view.NameText.text = model.Name;
            view.CreatorText.text = creators.GetDisplayName(model.Creator);
            view.CreatorPicture.Bind(creators.GetThumbnail(model.Creator));
            creatorCts = creatorCts.SafeRestart();
            ResolveCreatorAsync(model.Creator, creatorCts.Token).Forget();
            view.RarityBackground.sprite = rarityBackgrounds.GetTypeImage(model.Rarity);
            ShopRarityChip.Apply(view.RarityChipBackground, view.RarityChipText, styling.GetRarityFlapColor(model.Rarity));
            view.RarityChipText.text = ShopRaritySupply.ChipLabel(model.Rarity, model.Listing?.issuedId);
            view.CategoryIcon.sprite = styling.GetCategoryIcon(model.IsEmote ? ShopItemCardModel.CATEGORY_EMOTE : model.WearableCategory);
            view.SetPrice(model.IsNotForSale, model.PriceCredits);
            view.SetActions(presenter.ActionsFor(model));
            view.SetResolving(false);
            RefreshCartState();
            RefreshFavoriteState();

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(model.ThumbnailUrl, view.Thumbnail, view.DefaultThumbnail, actionsCts.Token, false).Forget();

            detailsCts = detailsCts.SafeRestart();
            LoadDetailsAsync(model, bodyShape, detailsCts.Token).Forget();
        }

        public void Hide()
        {
            detailsCts.SafeCancelAndDispose();
            creatorCts.SafeCancelAndDispose();
            actionsCts.SafeCancelAndDispose();
            Model = null;
            view.ClearHideChips();
            view.gameObject.SetActive(false);
        }

        public void RefreshCartState()
        {
            if (Model != null)
                view.SetCartState(presenter.IsInCart(Model), presenter.IsCartFull(Model));
        }

        private void RefreshFavoriteState()
        {
            if (Model == null)
                return;

            string? key = ShopFavorites.KeyOf(Model);
            view.SetFavoriteVisible(key != null);
            view.SetFavorite(key != null && favorites.IsFavorite(key));
        }

        private async UniTaskVoid LoadDetailsAsync(ShopItemCardModel model, BodyShape bodyShape, CancellationToken ct)
        {
            view.SetDetailsLoading(true);

            // Secondary listings without an item id build a URN that points to no entity
            if (string.IsNullOrEmpty(model.ItemId))
            {
                view.SetDetailsLoading(false);
                return;
            }

            CatalogItemDto? catalogItem = null;
            string? urn = model.Urn;

            // Ethereum collections (v1) have named URNs that cannot be derived from the listing, so they are read from the catalog row
            if (!ShopItemLinks.HasDerivableUrn(model.ChainId))
            {
                catalogItem = await FetchCatalogItemAsync(model, ct);
                urn = catalogItem?.urn;
            }

            if (ct.IsCancellationRequested)
                return;

            if (string.IsNullOrEmpty(urn))
            {
                SetPreviewUnavailable();
                view.SetDetailsLoading(false);
                return;
            }

            pointers.Clear();
            pointers.Add(urn);

            bool resolved = model.IsEmote
                ? await LoadEmoteDetailsAsync(urn, bodyShape, ct)
                : await LoadWearableDetailsAsync(urn, bodyShape, ct);

            if (ct.IsCancellationRequested)
                return;

            // The entity providers only know items the asset-bundle registry has converted; the catalog row describes every listed item
            if (!resolved)
            {
                catalogItem ??= await FetchCatalogItemAsync(model, ct);

                if (ct.IsCancellationRequested)
                    return;

                if (catalogItem != null)
                    view.SetDescription(catalogItem.Description());

                SetPreviewUnavailable();
            }

            view.SetDetailsLoading(false);
        }

        // Without an entity there is nothing to dress the avatar with or to play on it
        private void SetPreviewUnavailable()
        {
            view.SetPreviewUnavailable(true);
            PreviewUnavailable?.Invoke();
        }

        private async UniTask ResolveCreatorAsync(string address, CancellationToken ct)
        {
            creatorScratch.Clear();
            creatorScratch.Add(address);
            bool changed = await creators.ResolveAsync(creatorScratch, ct);

            if (!ct.IsCancellationRequested && changed)
                view.CreatorText.text = creators.GetDisplayName(address);
        }

        private async UniTask<CatalogItemDto?> FetchCatalogItemAsync(ShopItemCardModel model, CancellationToken ct)
        {
            catalogIds.Clear();
            catalogIds.Add($"{model.ContractAddress}-{model.ItemId}");
            Result<CatalogItemDto[]> result = await api.GetCatalogItemsByIdsAsync(catalogIds, ct).SuppressToResultAsync(ReportCategory.UI);

            return !ct.IsCancellationRequested && result.Success && result.Value.Length > 0 ? result.Value[0] : null;
        }

        private async UniTask<bool> LoadWearableDetailsAsync(URN urn, BodyShape bodyShape, CancellationToken ct)
        {
            wearablesBuffer.Clear();
            Result<IReadOnlyCollection<IWearable>?> result = await wearablesProvider.GetDTOsByPointersAsync(pointers, bodyShape, ct, wearablesBuffer).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested)
                return false;

            // The body shape rides along with the requested pointers, so the item is picked by its URN
            IWearable? wearable = result.Success && result.Value != null ? FindByUrn(result.Value, urn) : null;

            if (wearable == null)
                return false;

            view.SetDescription(wearable.GetDescription());

            hidingList.Clear();
            wearable.GetHidingList(bodyShape, hidingList);

            foreach (string category in hidingList)
                view.AddHideChip(WearableComponentsUtils.CATEGORIES_TO_READABLE.TryGetValue(category, out string label) ? label : category, styling.GetCategoryIcon(category));

            WearableResolved?.Invoke(wearable);
            return true;
        }

        private async UniTask<bool> LoadEmoteDetailsAsync(URN urn, BodyShape bodyShape, CancellationToken ct)
        {
            emotesBuffer.Clear();
            Result<IReadOnlyCollection<IEmote>?> result = await emoteProvider.GetByPointersAsync(pointers, bodyShape, ct, emotesBuffer).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested)
                return false;

            IEmote? emote = result.Success && result.Value != null ? FindByUrn(result.Value, urn) : null;

            if (emote == null)
                return false;

            view.SetDescription(emote.GetDescription());
            return true;
        }

        private static T? FindByUrn<T>(IReadOnlyCollection<T> candidates, URN urn) where T: class, IAvatarAttachment
        {
            foreach (T candidate in candidates)
            {
                if (candidate.GetUrn().Shorten().Equals(urn))
                    return candidate;
            }

            return null;
        }

        private void OnCloseClicked() =>
            CloseClicked?.Invoke();

        private void OnAddToCartClicked()
        {
            if (Model == null || view.IsResolving || actionsCts == null)
                return;

            presenter.AddToCartAsync(Model, cartSource, view.SetResolving, actionsCts.Token).Forget();
        }

        private void OnBuyClicked()
        {
            if (Model == null || view.IsResolving || actionsCts == null)
                return;

            presenter.BuyAsync(Model, view.Thumbnail.ImageSprite, buySource, view.SetResolving, actionsCts.Token).Forget();
        }

        private void OnMarketplaceClicked()
        {
            if (Model != null)
                presenter.View(Model);
        }

        private void OnFavoriteClicked()
        {
            if (Model != null)
                favorites.Toggle(Model, cartSource);
        }

        private void OnFavoriteChanged(string key, ShopItemCardModel model, bool isFavorite) =>
            RefreshFavoriteState();

        private void OnFavoritesLoaded(bool success) =>
            RefreshFavoriteState();
    }
}
