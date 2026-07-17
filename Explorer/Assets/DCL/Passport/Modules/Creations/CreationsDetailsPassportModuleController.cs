using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Fields;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Creations
{
    public class CreationsDetailsPassportModuleController : IPassportModuleController
    {
        private const int ITEMS_POOL_DEFAULT_CAPACITY = 8;
        private const int GRID_ITEMS_PER_ROW = 6;
        private const int EMPTY_ITEMS_POOL_DEFAULT_CAPACITY = (GRID_ITEMS_PER_ROW - 1) * 2;
        private const string WEARABLE_CATEGORY = "wearable";
        private const string EMOTE_CATEGORY = "emote";

        private readonly CreationsDetailsPassportModuleView view;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IWebBrowser webBrowser;
        private readonly ImageControllerProvider imageControllerProvider;
        private readonly PassportErrorsController passportErrorsController;
        private readonly IObjectPool<EquippedItemPassportFieldView> wearablesItemsPool;
        private readonly IObjectPool<EquippedItemPassportFieldView> emotesItemsPool;
        private readonly IObjectPool<EquippedItemPassportFieldView> emptyItemsPool;
        private readonly List<EquippedItemPassportFieldView> instantiatedWearables = new ();
        private readonly List<EquippedItemPassportFieldView> instantiatedEmotes = new ();
        private readonly List<EquippedItemPassportFieldView> instantiatedEmptyItems = new ();
        private readonly List<Texture2DRef> loadedThumbnails = new ();
        private readonly Dictionary<EquippedItemPassportFieldView, (UnityAction buy, UnityAction view)> navigationListeners = new ();
        private readonly CreditPurchaseBuyHandler creditPurchaseBuyHandler;

        private Profile? currentProfile;
        private CancellationTokenSource? loadCreationsCts;

        public CreationsDetailsPassportModuleController(
            CreationsDetailsPassportModuleView view,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IWebBrowser webBrowser,
            ImageControllerProvider imageControllerProvider,
            PassportErrorsController passportErrorsController,
            CreditPurchaseBuyHandler creditPurchaseBuyHandler)
        {
            this.view = view;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.webBrowser = webBrowser;
            this.imageControllerProvider = imageControllerProvider;
            this.passportErrorsController = passportErrorsController;
            this.creditPurchaseBuyHandler = creditPurchaseBuyHandler;

            wearablesItemsPool = CreateItemsPool(view.CreatedWearablesContainer);
            emotesItemsPool = CreateItemsPool(view.CreatedEmotesContainer);

            emptyItemsPool = new ObjectPool<EquippedItemPassportFieldView>(
                () => InstantiateItemPrefab(view.CreatedWearablesContainer),
                defaultCapacity: EMPTY_ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: emptyItemView =>
                {
                    emptyItemView.gameObject.SetActive(true);
                    emptyItemView.SetInvisible(true);
                },
                actionOnRelease: emptyItemView => emptyItemView.gameObject.SetActive(false));
        }

        private IObjectPool<EquippedItemPassportFieldView> CreateItemsPool(RectTransform parent) =>
            new ObjectPool<EquippedItemPassportFieldView>(
                () => InstantiateItemPrefab(parent),
                defaultCapacity: ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: itemView =>
                {
                    itemView.gameObject.SetActive(true);
                    itemView.gameObject.transform.SetAsLastSibling();
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
                    RemoveNavigationListener(itemView);
                    itemView.gameObject.SetActive(false);
                }
            );

        private EquippedItemPassportFieldView InstantiateItemPrefab(RectTransform parent) =>
            Object.Instantiate(view.EquippedItemPrefab, parent);

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            Clear();

            loadCreationsCts = loadCreationsCts.SafeRestart();

            view.MainLoadingSpinner.SetActive(true);
            view.NoCreationsLabel.SetActive(false);
            view.WearablesLabel.SetActive(false);
            view.EmotesLabel.SetActive(false);

            LoadCreationsAsync(loadCreationsCts.Token).Forget();
        }

        public void Clear()
        {
            loadCreationsCts.SafeCancelAndDispose();
            creditPurchaseBuyHandler.ClearCache();

            ClearItems(wearablesItemsPool, instantiatedWearables);
            ClearItems(emotesItemsPool, instantiatedEmotes);
            ClearItems(emptyItemsPool, instantiatedEmptyItems);

            foreach (Texture2DRef thumbnail in loadedThumbnails)
                thumbnail.Dispose();

            loadedThumbnails.Clear();
        }

        public void Dispose() =>
            Clear();

        private async UniTaskVoid LoadCreationsAsync(CancellationToken ct)
        {
            try
            {
                (int wearablesCount, int emotesCount) = await UniTask.WhenAll(
                    LoadCategoryAsync(WEARABLE_CATEGORY, wearablesItemsPool, instantiatedWearables, false, ct),
                    LoadCategoryAsync(EMOTE_CATEGORY, emotesItemsPool, instantiatedEmotes, true, ct));

                SetupUiElements(wearablesCount, emotesCount);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                view.MainLoadingSpinner.SetActive(false);
                const string ERROR_MESSAGE = "There was an error while loading the creations. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.UI, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void SetupUiElements(int wearablesCount, int emotesCount)
        {
            view.MainLoadingSpinner.SetActive(false);

            bool hasWearables = wearablesCount > 0;
            bool hasEmotes = emotesCount > 0;
            bool hasAnyCreation = hasWearables || hasEmotes;

            view.NoCreationsLabel.SetActive(!hasAnyCreation);
            view.CreatedWearablesContainer.gameObject.SetActive(hasWearables);
            view.CreatedEmotesContainer.gameObject.SetActive(hasEmotes);
            view.WearablesLabel.SetActive(hasWearables);
            view.EmotesLabel.SetActive(hasEmotes);

            if (hasWearables)
                AddEmptyItems(view.CreatedWearablesContainer, wearablesCount);

            if (hasEmotes)
                AddEmptyItems(view.CreatedEmotesContainer, emotesCount);
        }

        private void AddEmptyItems(RectTransform container, int realItemsCount)
        {
            int missingEmptyItems = CalculateMissingEmptyItems(realItemsCount);

            for (var i = 0; i < missingEmptyItems; i++)
            {
                EquippedItemPassportFieldView emptyItem = emptyItemsPool.Get();
                emptyItem.gameObject.name = "EmptyItem";
                emptyItem.transform.SetParent(container, false);
                emptyItem.transform.SetAsFirstSibling();
                instantiatedEmptyItems.Add(emptyItem);
            }
        }

        private static int CalculateMissingEmptyItems(int totalItems)
        {
            int remainder = totalItems % GRID_ITEMS_PER_ROW;
            return remainder == 0 ? 0 : GRID_ITEMS_PER_ROW - remainder;
        }

        private async UniTask<int> LoadCategoryAsync(
            string category,
            IObjectPool<EquippedItemPassportFieldView> pool,
            List<EquippedItemPassportFieldView> instantiatedItems,
            bool isEmote,
            CancellationToken ct)
        {
            string baseUrl = decentralandUrlsSource.Url(DecentralandUrl.MarketplaceServer);
            var url = URLAddress.FromString($"{baseUrl}/v2/catalog?category={category}&creator={currentProfile?.UserId}&includeSocialEmotes=false&first=100");

            MarketplaceCatalogResponse response = await webRequestController.GetAsync(url, ct, ReportCategory.UI)
                                                                            .CreateFromJson<MarketplaceCatalogResponse>(WRJsonParser.Unity);

            if (response?.data == null)
                return 0;

            foreach (MarketplaceCatalogItem item in response.data)
            {
                EquippedItemPassportFieldView itemView = pool.Get();
                SetupItemView(itemView, item, isEmote, ct);
                instantiatedItems.Add(itemView);
            }

            return response.data.Length;
        }

        private void SetupItemView(EquippedItemPassportFieldView itemView, MarketplaceCatalogItem item, bool isEmote, CancellationToken ct)
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
            itemView.CategoryImage.sprite = categoryIcons.GetTypeImage(isEmote ? EMOTE_CATEGORY : item.data?.wearable?.category);

            string marketplaceLink = GetMarketplaceLink(item);
            bool hasLink = marketplaceLink != string.Empty;
            bool isBaseWearable = itemView.ItemId.IsBaseWearable();
            bool showBuy = !isBaseWearable && item.isOnSale && hasLink;
            bool showView = !isBaseWearable && !item.isOnSale && hasLink;

            itemView.BuyButton.gameObject.SetActive(showBuy);
            itemView.ViewButton.gameObject.SetActive(showView);
            itemView.OnSaleFlap.gameObject.SetActive(showBuy);

            RemoveNavigationListener(itemView);
            string itemUrn = item.urn ?? string.Empty;
            string rarityName = item.rarity ?? string.Empty;
            UnityAction buyListener = () => OnBuyClicked(itemView, itemUrn, marketplaceLink, rarityName, raritySprite, rarityColor);
            UnityAction viewListener = () => webBrowser.OpenUrl(marketplaceLink);
            itemView.BuyButton.onClick.AddListener(buyListener);
            itemView.ViewButton.onClick.AddListener(viewListener);
            navigationListeners[itemView] = (buyListener, viewListener);

            itemView.SetAsLoading(false);
            WaitForThumbnailAsync(item.thumbnail, itemView, ct).Forget();
        }

        private void RemoveNavigationListener(EquippedItemPassportFieldView itemView)
        {
            if (!navigationListeners.TryGetValue(itemView, out (UnityAction buy, UnityAction view) listeners))
                return;

            itemView.BuyButton.onClick.RemoveListener(listeners.buy);
            itemView.ViewButton.onClick.RemoveListener(listeners.view);
            navigationListeners.Remove(itemView);
        }

        private void OnBuyClicked(EquippedItemPassportFieldView itemView, string urn, string marketplaceLink, string rarityName, Sprite raritySprite, Color rarityColor)
        {
            if (loadCreationsCts == null)
                return;

            var visuals = new CreditPurchaseBuyHandler.ItemVisuals(
                itemView.AssetNameText.text,
                rarityName,
                itemView.EquippedItemThumbnail.sprite,
                raritySprite,
                rarityColor,
                itemView.CategoryImage.sprite);

            creditPurchaseBuyHandler.HandleBuyClickAsync(
                                         urn, marketplaceLink, visuals,
                                         resolving => itemView.BuyButton.interactable = !resolving,
                                         loadCreationsCts.Token)
                                    .Forget();
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
                ReportHub.LogError(ReportCategory.UI, $"There was an error while trying to load a creation thumbnail. ERROR: {e.Message}");
            }
        }

        private static void ClearItems(IObjectPool<EquippedItemPassportFieldView> pool, List<EquippedItemPassportFieldView> instantiatedItems)
        {
            foreach (EquippedItemPassportFieldView item in instantiatedItems)
                pool.Release(item);

            instantiatedItems.Clear();
        }
    }
}
