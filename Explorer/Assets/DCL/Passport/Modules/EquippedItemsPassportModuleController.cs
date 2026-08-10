using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.Browser.DecentralandUrls;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Fields;
using DCL.Passport.Modules.Creations;
using DCL.Profiles;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules
{
    public class EquippedItemsPassportModuleController : IPassportModuleController
    {
        private const int EQUIPPED_ITEMS_POOL_DEFAULT_CAPACITY = 28;
        private const int LOADING_ITEMS_POOL_DEFAULT_CAPACITY = 12;
        private const int GRID_ITEMS_PER_ROW = 6;

        private readonly EquippedItemsPassportModuleView view;
        private readonly World world;
        private readonly IWebRequestController webRequestController;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly PassportErrorsController passportErrorsController;
        private readonly IObjectPool<EquippedItemPassportFieldView> loadingItemsPool;
        private readonly List<EquippedItemPassportFieldView> instantiatedLoadingItems = new ();
        private readonly IObjectPool<EquippedItemPassportFieldView> equippedItemsPool;
        private readonly List<EquippedItemPassportFieldView> instantiatedEquippedItems = new ();
        private readonly IObjectPool<EquippedItemPassportFieldView> emptyItemsPool;
        private readonly List<EquippedItemPassportFieldView> instantiatedEmptyItems = new ();
        private readonly CreditPurchaseBuyHandler creditPurchaseBuyHandler;
        private readonly List<(EquippedItemPassportFieldView view, string urn)> primaryListingCandidates = new ();
        private readonly HashSet<string> onSaleUrns = new (StringComparer.OrdinalIgnoreCase);

        private Profile currentProfile;
        private CancellationTokenSource getEquippedItemsCts;

        public EquippedItemsPassportModuleController(
            EquippedItemsPassportModuleView view,
            World world,
            IWebRequestController webRequestController,
            UnityAppWebBrowser webBrowser,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IThumbnailProvider thumbnailProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            PassportErrorsController passportErrorsController,
            CreditPurchaseBuyHandler creditPurchaseBuyHandler)
        {
            this.view = view;
            this.world = world;
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.thumbnailProvider = thumbnailProvider;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.passportErrorsController = passportErrorsController;
            this.creditPurchaseBuyHandler = creditPurchaseBuyHandler;

            loadingItemsPool = new ObjectPool<EquippedItemPassportFieldView>(
                InstantiateEquippedItemPrefab,
                defaultCapacity: LOADING_ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: loadingItemView =>
                {
                    loadingItemView.gameObject.SetActive(true);
                    loadingItemView.gameObject.transform.SetAsLastSibling();
                    loadingItemView.SetAsLoading(true);
                },
                actionOnRelease: loadingItemView =>
                {
                    loadingItemView.SetAsLoading(false);
                    loadingItemView.gameObject.SetActive(false);
                }
            );

            equippedItemsPool = new ObjectPool<EquippedItemPassportFieldView>(
                InstantiateEquippedItemPrefab,
                defaultCapacity: EQUIPPED_ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: equippedItemView =>
                {
                    equippedItemView.gameObject.SetActive(true);
                    equippedItemView.gameObject.transform.SetAsFirstSibling();
                },
                actionOnRelease: equippedItemView =>
                {
                    equippedItemView.gameObject.SetActive(false);
                    equippedItemView.BuyButton.onClick.RemoveAllListeners();
                    equippedItemView.ViewButton.onClick.RemoveAllListeners();
                });

            emptyItemsPool = new ObjectPool<EquippedItemPassportFieldView>(
                InstantiateEquippedItemPrefab,
                defaultCapacity: GRID_ITEMS_PER_ROW - 1,
                actionOnGet: emptyItemView =>
                {
                    emptyItemView.gameObject.SetActive(true);
                    emptyItemView.SetInvisible(true);
                    emptyItemView.gameObject.transform.SetAsFirstSibling();
                },
                actionOnRelease: emptyItemView => emptyItemView.gameObject.SetActive(false));
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            LoadEquippedItems();
        }

        public void Clear()
        {
            getEquippedItemsCts.SafeCancelAndDispose();
            creditPurchaseBuyHandler.ClearCache();
            primaryListingCandidates.Clear();
            ClearLoadingItems();
            ClearEquippedItems();
            ClearEmptyItems();
        }

        public void Dispose() =>
            Clear();

        private EquippedItemPassportFieldView InstantiateEquippedItemPrefab()
        {
            EquippedItemPassportFieldView equippedItemView = Object.Instantiate(view.equippedItemPrefab, view.EquippedItemsContainer);
            return equippedItemView;
        }

        private void LoadEquippedItems()
        {
            Clear();
            SetGridAsLoading();

            WearablePromise equippedWearablesPromise = WearablePromise.Create(
                world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(currentProfile.Avatar.BodyShape, currentProfile.Avatar.Wearables, currentProfile.Avatar.ForceRender),
                PartitionComponent.TOP_PRIORITY);

            EmotePromise equippedEmotesPromise = EmotePromise.Create(
                world,
                EmoteComponentsUtils.CreateGetEmotesByPointersIntention(currentProfile.Avatar.BodyShape, currentProfile.Avatar.Emotes),
                PartitionComponent.TOP_PRIORITY);

            getEquippedItemsCts = getEquippedItemsCts.SafeRestart();
            AwaitEquippedItemsPromiseAsync(equippedWearablesPromise, equippedEmotesPromise, getEquippedItemsCts.Token).Forget();
        }

        private void SetGridAsLoading()
        {
            for (var i = 0; i < LOADING_ITEMS_POOL_DEFAULT_CAPACITY; i++)
            {
                var loadingItem = loadingItemsPool.Get();
                loadingItem.gameObject.name = "LoadingItem";
                instantiatedLoadingItems.Add(loadingItem);
            }
        }

        private void SetGridElements(List<IWearable> gridWearables, IReadOnlyList<IEmote> gridEmotes)
        {
            ClearLoadingItems();

            HashSet<string> hidesList = Wearable.ComposeHiddenCategories(currentProfile.Avatar.BodyShape, gridWearables, currentProfile.Avatar.ForceRender);
            var elementsAddedInTheGird = 0;

            foreach (IWearable wearable in gridWearables)
            {
                if (wearable.GetCategory() == WearableCategories.Categories.BODY_SHAPE)
                    continue;

                string wearableCategory = wearable.GetCategory();
                if (hidesList.Contains(wearableCategory))
                    continue;

                string rarityName = wearable.GetRarity();
                Sprite raritySprite = rarityBackgrounds.GetTypeImage(rarityName);
                Color rarityColor = rarityColors.GetColor(rarityName);

                var equippedWearableItem = equippedItemsPool.Get();
                equippedWearableItem.AssetNameText.text = wearable.GetName();
                equippedWearableItem.ItemId = wearable.GetUrn();
                equippedWearableItem.RarityBackground.sprite = raritySprite;
                equippedWearableItem.RarityLabelText.text = rarityName;
                equippedWearableItem.RarityLabelText.color = rarityColor;
                equippedWearableItem.RarityBackground2.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, equippedWearableItem.RarityBackground2.color.a);
                equippedWearableItem.FlapBackground.color = rarityColor;
                equippedWearableItem.CategoryImage.sprite = categoryIcons.GetTypeImage(wearable.GetCategory());
                string marketPlaceLink = GetMarketplaceLink(wearable.GetUrn());
                equippedWearableItem.BuyButton.gameObject.SetActive(false);
                equippedWearableItem.ViewButton.gameObject.SetActive(false);
                equippedWearableItem.OnSaleFlap.gameObject.SetActive(false);
                string wearableUrn = wearable.GetUrn();
                var wearableItemView = equippedWearableItem;
                equippedWearableItem.BuyButton.onClick.AddListener(() => OnBuyClicked(wearableItemView, wearableUrn, marketPlaceLink, rarityName, raritySprite, rarityColor));

                if (wearable.IsOnChain() && marketPlaceLink != string.Empty)
                {
                    equippedWearableItem.ViewButton.onClick.AddListener(() => webBrowser.OpenUrlMainThreadOnly(marketPlaceLink));
                    primaryListingCandidates.Add((equippedWearableItem, wearableUrn));
                }

                WaitForThumbnailAsync(wearable, equippedWearableItem, getEquippedItemsCts.Token).Forget();
                instantiatedEquippedItems.Add(equippedWearableItem);
                elementsAddedInTheGird++;
            }

            foreach (IEmote emote in gridEmotes)
            {
                string rarityName = emote.GetRarity();
                Sprite raritySprite = rarityBackgrounds.GetTypeImage(rarityName);
                Color rarityColor = rarityColors.GetColor(rarityName);

                var equippedWearableItem = equippedItemsPool.Get();
                equippedWearableItem.AssetNameText.text = emote.GetName();
                equippedWearableItem.ItemId = emote.GetUrn();
                equippedWearableItem.RarityBackground.sprite = raritySprite;
                equippedWearableItem.RarityLabelText.text = rarityName;
                equippedWearableItem.RarityLabelText.color = rarityColor;
                equippedWearableItem.RarityBackground2.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, equippedWearableItem.RarityBackground2.color.a);
                equippedWearableItem.FlapBackground.color = rarityColor;
                equippedWearableItem.CategoryImage.sprite = categoryIcons.GetTypeImage("emote");
                string marketPlaceLink = GetMarketplaceLink(emote.GetUrn());
                equippedWearableItem.BuyButton.gameObject.SetActive(false);
                equippedWearableItem.ViewButton.gameObject.SetActive(false);
                equippedWearableItem.OnSaleFlap.gameObject.SetActive(false);
                string emoteUrn = emote.GetUrn();
                var emoteItemView = equippedWearableItem;
                equippedWearableItem.BuyButton.onClick.AddListener(() => OnBuyClicked(emoteItemView, emoteUrn, marketPlaceLink, rarityName, raritySprite, rarityColor));

                if (emote.IsOnChain() && rarityName != "base" && marketPlaceLink != string.Empty)
                {
                    equippedWearableItem.ViewButton.onClick.AddListener(() => webBrowser.OpenUrlMainThreadOnly(marketPlaceLink));
                    primaryListingCandidates.Add((equippedWearableItem, emoteUrn));
                }

                WaitForThumbnailAsync(emote, equippedWearableItem, getEquippedItemsCts.Token).Forget();
                instantiatedEquippedItems.Add(equippedWearableItem);
                elementsAddedInTheGird++;
            }

            int missingEmptyItems = CalculateMissingEmptyItems(elementsAddedInTheGird);

            for (var i = 0; i < missingEmptyItems; i++)
            {
                var emptyItem = emptyItemsPool.Get();
                emptyItem.gameObject.name = "EmptyItem";
                instantiatedEmptyItems.Add(emptyItem);
            }

            if (primaryListingCandidates.Count > 0)
                ResolvePrimaryListingsAsync(getEquippedItemsCts.Token).Forget();
        }

        private async UniTaskVoid ResolvePrimaryListingsAsync(CancellationToken ct)
        {
            try
            {
                URLAddress url;

                using (decentralandUrlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder))
                {
                    urlBuilder.AppendPath(new URLPath("v2/catalog"));
                    urlBuilder.AppendParameter(new URLParameter("first", primaryListingCandidates.Count.ToString()));

                    foreach ((_, string urn) in primaryListingCandidates)
                        urlBuilder.AppendParameter(new URLParameter("urn", urn));

                    url = urlBuilder.Build();
                }

                MarketplaceCatalogResponse response = await webRequestController.GetAsync(url, ct, ReportCategory.WEARABLE)
                                                                                .CreateFromJson<MarketplaceCatalogResponse>(WRJsonParser.Unity);

                if (ct.IsCancellationRequested)
                    return;

                onSaleUrns.Clear();

                if (response?.data != null)
                    foreach (MarketplaceCatalogItem item in response.data)
                        if (item is { isOnSale: true, urn: not null })
                            onSaleUrns.Add(item.urn);

                foreach ((EquippedItemPassportFieldView itemView, string urn) in primaryListingCandidates)
                {
                    bool isOnPrimarySale = onSaleUrns.Contains(urn);
                    itemView.BuyButton.gameObject.SetActive(isOnPrimarySale);
                    itemView.OnSaleFlap.gameObject.SetActive(isOnPrimarySale);
                    itemView.ViewButton.gameObject.SetActive(!isOnPrimarySale);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.WEARABLE, $"There was an error while resolving primary listings for equipped items. ERROR: {e.Message}");

                if (ct.IsCancellationRequested)
                    return;

                foreach ((EquippedItemPassportFieldView itemView, _) in primaryListingCandidates)
                    itemView.ViewButton.gameObject.SetActive(true);
            }
        }

        private static int CalculateMissingEmptyItems(int totalItems)
        {
            int remainder = totalItems % GRID_ITEMS_PER_ROW;
            int missingItems = remainder == 0 ? 0 : GRID_ITEMS_PER_ROW - remainder;
            return missingItems;
        }

        private async UniTaskVoid AwaitEquippedItemsPromiseAsync(
            WearablePromise equippedWearablesPromise,
            EmotePromise equippedEmotesPromise,
            CancellationToken ct
        )
        {
            try
            {
                var wearablesUniTaskAsync = await equippedWearablesPromise.ToUniTaskAsync(world, cancellationToken: ct);
                var emotesUniTaskAsync = await equippedEmotesPromise.ToUniTaskAsync(world, cancellationToken: ct);
                var currentWearables = wearablesUniTaskAsync.Result!.Value.Asset.Wearables;
                using var consumed = emotesUniTaskAsync.Result!.Value.Asset.ConsumeEmotes();
                SetGridElements(currentWearables, consumed.Value);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to load the equipped items. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.WEARABLE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable itemWearable, EquippedItemPassportFieldView itemView, CancellationToken ct)
        {
            try
            {
                var sprite = await thumbnailProvider.GetAsync(itemWearable, ct);
                itemView.EquippedItemThumbnail.sprite = sprite;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                itemView.EquippedItemThumbnail.sprite = null;
                const string ERROR_MESSAGE = "There was an error while trying to load wearable thumbnails. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.WEARABLE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private async UniTaskVoid WaitForThumbnailAsync(IEmote itemEmote, EquippedItemPassportFieldView itemView, CancellationToken ct)
        {
            try
            {
                Sprite sprite = await thumbnailProvider.GetAsync(itemEmote, ct);
                itemView.EquippedItemThumbnail.sprite = sprite;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                itemView.EquippedItemThumbnail.sprite = null;
                const string ERROR_MESSAGE = "There was an error while trying to load emote thumbnails. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.WEARABLE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void ClearLoadingItems()
        {
            foreach (EquippedItemPassportFieldView loadingItem in instantiatedLoadingItems)
                loadingItemsPool.Release(loadingItem);

            instantiatedLoadingItems.Clear();
        }

        private void ClearEquippedItems()
        {
            foreach (EquippedItemPassportFieldView equippedItem in instantiatedEquippedItems)
                equippedItemsPool.Release(equippedItem);

            instantiatedEquippedItems.Clear();
        }

        private void ClearEmptyItems()
        {
            foreach (EquippedItemPassportFieldView emptyItem in instantiatedEmptyItems)
                emptyItemsPool.Release(emptyItem);

            instantiatedEmptyItems.Clear();
        }

        private void OnBuyClicked(EquippedItemPassportFieldView itemView, string urn, string marketplaceLink, string rarityName, Sprite raritySprite, Color rarityColor)
        {
            var visuals = new CreditPurchaseBuyHandler.ItemVisuals(
                itemView.AssetNameText.text,
                rarityName,
                itemView.EquippedItemThumbnail.sprite,
                raritySprite,
                rarityColor,
                itemView.CategoryImage.sprite);

            creditPurchaseBuyHandler.HandleBuyClickAsync(
                                         urn, marketplaceLink, visuals,
                                         CreditPurchaseModalControllerParams.SOURCE_PASSPORT_EQUIPPED,
                                         resolving => itemView.BuyButton.interactable = !resolving,
                                         getEquippedItemsCts.Token)
                                    .Forget();
        }

        private string GetMarketplaceLink(string id)
        {
            var marketplace = $"{decentralandUrlsSource.Url(DecentralandUrl.ShopLink)}/item/{{0}}/{{1}}";
            ReadOnlySpan<char> idSpan = id.AsSpan();
            int lastColonIndex = idSpan.LastIndexOf(':');

            if (lastColonIndex == -1)
                return "";

            var item = idSpan.Slice(lastColonIndex + 1).ToString();
            idSpan = idSpan.Slice(0, lastColonIndex);
            int secondLastColonIndex = idSpan.LastIndexOf(':');
            var contract = idSpan.Slice(secondLastColonIndex + 1).ToString();

            // If this is not correct, we could retrieve the marketplace link by checking TheGraph, but that's super slow
            if (!contract.StartsWith("0x") || !int.TryParse(item, out int _))
                return "";

            // Tagged here rather than on the ShopLink constant: the format string above appends a path to it,
            // so a query string on the constant would land before the path and 404. See ClientSourceUrlExtensions.
            return string.Format(marketplace, contract, item).WithClientSource();
        }
    }
}
