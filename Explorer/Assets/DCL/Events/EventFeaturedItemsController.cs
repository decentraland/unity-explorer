using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Backpack.Gifting.Utils;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Fields;
using DCL.Passport.Modules;
using DCL.Passport.Modules.Creations;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Communities.EventInfo
{
    public class EventFeaturedItemsController : IDisposable
    {
        private const int ITEMS_POOL_DEFAULT_CAPACITY = 8;
        private const int COLLECTION_ITEMS_LIMIT = 20;
        private const string URN_PREFIX = "urn:";
        private const string CONTRACT_ADDRESS_PREFIX = "0x";
        private const string EMOTE_CATEGORY = "emote";

        private readonly EventFeaturedItemsView view;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly ImageControllerProvider imageControllerProvider;
        private readonly CreditPurchaseBuyHandler creditPurchaseBuyHandler;
        private readonly bool isFeatureEnabled;
        private readonly IObjectPool<EquippedItemPassportFieldView> itemsPool;
        private readonly List<EquippedItemPassportFieldView> instantiatedItems = new ();
        private readonly List<Texture2DRef> loadedThumbnails = new ();
        private readonly Dictionary<EquippedItemPassportFieldView, MarketplaceCatalogItem> boundItems = new ();

        private CancellationToken showCt;

        public EventFeaturedItemsController(
            EventFeaturedItemsView view,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            UnityAppWebBrowser webBrowser,
            ImageControllerProvider imageControllerProvider,
            CreditPurchaseBuyHandler creditPurchaseBuyHandler,
            bool isFeatureEnabled)
        {
            this.view = view;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.webBrowser = webBrowser;
            this.imageControllerProvider = imageControllerProvider;
            this.creditPurchaseBuyHandler = creditPurchaseBuyHandler;
            this.isFeatureEnabled = isFeatureEnabled;

            itemsPool = new ObjectPool<EquippedItemPassportFieldView>(
                InstantiateItemPrefab,
                defaultCapacity: ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: itemView =>
                {
                    itemView.gameObject.SetActive(true);
                    itemView.gameObject.transform.SetAsLastSibling();
                    itemView.ItemPriceContainer.SetActive(false);
                    itemView.SetAsLoading(true);
                },
                actionOnRelease: itemView =>
                {
                    if (itemView.EquippedItemThumbnail.sprite != null)
                    {
                        Object.Destroy(itemView.EquippedItemThumbnail.sprite);
                        itemView.EquippedItemThumbnail.sprite = null;
                    }

                    itemView.SetAsLoading(false);
                    boundItems.Remove(itemView);
                    itemView.gameObject.SetActive(false);
                });
        }

        public void Dispose() =>
            Clear();

        public void Show(string? featuredItemUrn, CancellationToken ct)
        {
            Clear();

            if (!isFeatureEnabled || string.IsNullOrEmpty(featuredItemUrn))
            {
                view.Root.SetActive(false);
                return;
            }

            showCt = ct;
            view.Root.SetActive(true);
            view.LoadingSpinner.SetActive(true);
            LoadItemsAsync(featuredItemUrn, ct).Forget();
        }

        public void Clear()
        {
            creditPurchaseBuyHandler.ClearCache();

            foreach (EquippedItemPassportFieldView item in instantiatedItems)
                itemsPool.Release(item);

            instantiatedItems.Clear();

            foreach (Texture2DRef thumbnail in loadedThumbnails)
                thumbnail.Dispose();

            loadedThumbnails.Clear();
        }

        public static bool TryBuildCatalogUrl(string marketplaceServerBaseUrl, string featuredItemUrn, out URLAddress url)
        {
            url = URLAddress.EMPTY;

            if (!featuredItemUrn.StartsWith(URN_PREFIX, StringComparison.OrdinalIgnoreCase))
                return false;

            if (CreditPurchaseBuyHandler.TryParseCollectionItem(featuredItemUrn, out string _, out string _))
            {
                url = URLAddress.FromString($"{marketplaceServerBaseUrl}/v3/catalog/items?first=1&urn={featuredItemUrn}");
                return true;
            }

            int lastColonIndex = featuredItemUrn.LastIndexOf(':');
            ReadOnlySpan<char> lastSegment = featuredItemUrn.AsSpan(lastColonIndex + 1);

            if (!lastSegment.StartsWith(CONTRACT_ADDRESS_PREFIX) || !GiftingUrnParsingHelper.TryGetContractAddress(featuredItemUrn, out string contractAddress))
                return false;

            url = URLAddress.FromString($"{marketplaceServerBaseUrl}/v3/catalog/items?contractAddress={contractAddress}&first={COLLECTION_ITEMS_LIMIT}");
            return true;
        }

        private EquippedItemPassportFieldView InstantiateItemPrefab()
        {
            EquippedItemPassportFieldView itemView = Object.Instantiate(view.ItemPrefab, view.ItemsContainer);
            itemView.BuyButton.onClick.AddListener(() => OnBuyClicked(itemView));
            itemView.ViewButton.onClick.AddListener(() => OnViewClicked(itemView));
            return itemView;
        }

        private async UniTaskVoid LoadItemsAsync(string featuredItemUrn, CancellationToken ct)
        {
            try
            {
                if (!TryBuildCatalogUrl(decentralandUrlsSource.Url(DecentralandUrl.MarketplaceServer), featuredItemUrn, out URLAddress url))
                {
                    ReportHub.LogWarning(ReportCategory.EVENTS, $"Unrecognized event featured item urn: {featuredItemUrn}");
                    view.Root.SetActive(false);
                    return;
                }

                MarketplaceCatalogResponse response = await webRequestController.GetAsync(url, ct, ReportCategory.EVENTS)
                                                                                .CreateFromJson<MarketplaceCatalogResponse>(WRJsonParser.Unity);

                view.LoadingSpinner.SetActive(false);

                if (response?.data == null || response.data.Length == 0)
                {
                    view.Root.SetActive(false);
                    return;
                }

                foreach (MarketplaceCatalogItem item in response.data)
                {
                    EquippedItemPassportFieldView itemView = itemsPool.Get();
                    SetupItemView(itemView, item, ct);
                    instantiatedItems.Add(itemView);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                view.Root.SetActive(false);
                ReportHub.LogError(ReportCategory.EVENTS, $"There was an error while loading the event featured items. ERROR: {e.Message}");
            }
        }

        private void SetupItemView(EquippedItemPassportFieldView itemView, MarketplaceCatalogItem item, CancellationToken ct)
        {
            Sprite raritySprite = rarityBackgrounds.GetTypeImage(item.rarity);
            Color rarityColor = rarityColors.GetColor(item.rarity);

            itemView.AssetNameText.text = item.name ?? string.Empty;
            itemView.ItemId = item.urn ?? string.Empty;
            itemView.RarityBackground.sprite = raritySprite;
            itemView.RarityLabelText.text = item.rarity ?? string.Empty;
            itemView.RarityLabelText.color = rarityColor;
            itemView.RarityBackground2.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, itemView.RarityBackground2.color.a);
            itemView.FlapBackground.color = rarityColor;
            itemView.CategoryImage.sprite = categoryIcons.GetTypeImage(item.category == EMOTE_CATEGORY ? EMOTE_CATEGORY : item.data?.wearable?.category);

            string marketplaceLink = GetMarketplaceLink(item);
            bool hasLink = marketplaceLink != string.Empty;
            bool showBuy = item.isOnSale && hasLink;
            bool showView = !item.isOnSale && hasLink;

            itemView.BuyButton.gameObject.SetActive(showBuy);
            itemView.ViewButton.gameObject.SetActive(showView);
            itemView.OnSaleFlap.gameObject.SetActive(showBuy);

            bool showPrice = showBuy && item.priceCredits > 0;
            itemView.ItemPriceContainer.SetActive(showPrice);

            if (showPrice)
                itemView.ItemPrice.text = item.priceCredits.ToString();

            boundItems[itemView] = item;

            itemView.SetAsLoading(false);
            WaitForThumbnailAsync(item.thumbnail, itemView, ct).Forget();
        }

        private void OnBuyClicked(EquippedItemPassportFieldView itemView)
        {
            if (!boundItems.TryGetValue(itemView, out MarketplaceCatalogItem item))
                return;

            Sprite raritySprite = rarityBackgrounds.GetTypeImage(item.rarity);
            Color rarityColor = rarityColors.GetColor(item.rarity);

            var visuals = new CreditPurchaseBuyHandler.ItemVisuals(
                item.name ?? string.Empty,
                item.rarity ?? string.Empty,
                itemView.EquippedItemThumbnail.sprite,
                raritySprite,
                rarityColor,
                itemView.CategoryImage.sprite);

            creditPurchaseBuyHandler.HandleBuyClickAsync(
                                         item.urn ?? string.Empty, GetMarketplaceLink(item), visuals,
                                         CreditPurchaseModalControllerParams.SOURCE_EVENT_FEATURED_ITEMS,
                                         resolving => itemView.BuyButton.interactable = !resolving,
                                         showCt)
                                    .Forget();
        }

        private void OnViewClicked(EquippedItemPassportFieldView itemView)
        {
            if (!boundItems.TryGetValue(itemView, out MarketplaceCatalogItem item))
                return;

            string marketplaceLink = GetMarketplaceLink(item);

            if (marketplaceLink != string.Empty)
                webBrowser.OpenUrlMainThreadOnly(marketplaceLink);
        }

        private string GetMarketplaceLink(MarketplaceCatalogItem item)
        {
            if (string.IsNullOrEmpty(item.url))
                return string.Empty;

            return $"{decentralandUrlsSource.Url(DecentralandUrl.Market)}{item.url}";
        }

        private async UniTaskVoid WaitForThumbnailAsync(string? thumbnailUrl, EquippedItemPassportFieldView itemView, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(thumbnailUrl))
                return;

            try
            {
                Texture2DRef? textureRef = await imageControllerProvider.LoadTextureAsync(thumbnailUrl, ct);

                if (!textureRef.HasValue || ct.IsCancellationRequested)
                {
                    textureRef?.Dispose();
                    return;
                }

                loadedThumbnails.Add(textureRef.Value);
                Texture2D texture = textureRef.Value.Texture;
                itemView.EquippedItemThumbnail.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                itemView.EquippedItemThumbnail.sprite = null;
                ReportHub.LogError(ReportCategory.EVENTS, $"There was an error while trying to load a featured item thumbnail. ERROR: {e.Message}");
            }
        }
    }
}
