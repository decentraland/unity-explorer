using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Passport.Fields;
using DCL.Profiles;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
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
    public class EquippedItems_PassportModuleController : IPassportModuleController
    {
        private const int EQUIPPED_ITEMS_POOL_DEFAULT_CAPACITY = 28;
        private const int LOADING_ITEMS_POOL_DEFAULT_CAPACITY = 12;
        private const int GRID_ITEMS_PER_ROW = 6;

        private readonly EquippedItems_PassportModuleView view;
        private readonly World world;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly RectTransform scrollContainer;
        private readonly IWebBrowser webBrowser;
        private readonly PassportErrorsController passportErrorsController;

        private readonly IObjectPool<EquippedItem_PassportFieldView> loadingItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedLoadingItems = new();
        private readonly IObjectPool<EquippedItem_PassportFieldView> equippedItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedEquippedItems = new();
        private readonly IObjectPool<EquippedItem_PassportFieldView> emptyItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedEmptyItems = new();

        private Profile currentProfile;
        private CancellationTokenSource getEquippedItemsCts;

        public EquippedItems_PassportModuleController(
            EquippedItems_PassportModuleView view,
            World world,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IThumbnailProvider thumbnailProvider,
            RectTransform scrollContainer,
            IWebBrowser webBrowser,
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.world = world;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.thumbnailProvider = thumbnailProvider;
            this.scrollContainer = scrollContainer;
            this.webBrowser = webBrowser;
            this.passportErrorsController = passportErrorsController;

            loadingItemsPool = new ObjectPool<EquippedItem_PassportFieldView>(
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

            equippedItemsPool = new ObjectPool<EquippedItem_PassportFieldView>(
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
                });

            emptyItemsPool = new ObjectPool<EquippedItem_PassportFieldView>(
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
            ClearLoadingItems();
            ClearEquippedItems();
            ClearEmptyItems();
        }

        public void Dispose()
        {
            getEquippedItemsCts.SafeCancelAndDispose();
            Clear();
        }

        private EquippedItem_PassportFieldView InstantiateEquippedItemPrefab()
        {
            EquippedItem_PassportFieldView equippedItemView = Object.Instantiate(view.equippedItemPrefab, view.EquippedItemsContainer);
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

            HashSet<string> hidesList = Wearable.ComposeHiddenCategories(currentProfile.Avatar.BodyShape, gridWearables);
            var elementsAddedInTheGird = 0;

            foreach (IWearable wearable in gridWearables)
            {
                if (wearable.GetCategory() == WearablesConstants.Categories.BODY_SHAPE)
                    continue;

                if (hidesList.Contains(wearable.GetCategory()))
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
                equippedWearableItem.BuyButton.interactable = wearable.IsCollectible() && marketPlaceLink != string.Empty;
                equippedWearableItem.BuyButton.onClick.AddListener(() => webBrowser.OpenUrl(marketPlaceLink));
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
                equippedWearableItem.BuyButton.interactable = emote.IsCollectible() && rarityName != "base" && marketPlaceLink != string.Empty;
                equippedWearableItem.BuyButton.onClick.AddListener(() => webBrowser.OpenUrl(marketPlaceLink));
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
        }

        private int CalculateMissingEmptyItems(int totalItems)
        {
            int remainder = totalItems % 6;
            int missingItems = remainder == 0 ? 0 : 6 - remainder;
            return missingItems;
        }

        private async UniTaskVoid AwaitEquippedItemsPromiseAsync(WearablePromise equippedWearablesPromise, EmotePromise equippedEmotesPromise, CancellationToken ct)
        {
            try
            {
                var wearablesUniTaskAsync = await equippedWearablesPromise.ToUniTaskAsync(world, cancellationToken: ct);
                var emotesUniTaskAsync = await equippedEmotesPromise.ToUniTaskAsync(world, cancellationToken: ct);
                var currentWearables = wearablesUniTaskAsync.Result!.Value.Asset.Wearables;
                var currentEmotes = emotesUniTaskAsync.Result!.Value.Asset.Emotes;
                SetGridElements(currentWearables, currentEmotes);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to load the equipped items. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable itemWearable, EquippedItem_PassportFieldView itemView, CancellationToken ct)
        {
            try
            {
                Sprite sprite = await thumbnailProvider.GetAsync(itemWearable, ct);
                itemView.EquippedItemThumbnail.sprite = sprite;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                itemView.EquippedItemThumbnail.sprite = null;
                const string ERROR_MESSAGE = "There was an error while trying to load wearable thumbnails. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private async UniTaskVoid WaitForThumbnailAsync(IEmote itemEmote, EquippedItem_PassportFieldView itemView, CancellationToken ct)
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
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void ClearLoadingItems()
        {
            foreach (EquippedItem_PassportFieldView loadingItem in instantiatedLoadingItems)
                loadingItemsPool.Release(loadingItem);

            instantiatedLoadingItems.Clear();
        }

        private void ClearEquippedItems()
        {
            foreach (EquippedItem_PassportFieldView equippedItem in instantiatedEquippedItems)
                equippedItemsPool.Release(equippedItem);

            instantiatedEquippedItems.Clear();
        }

        private void ClearEmptyItems()
        {
            foreach (EquippedItem_PassportFieldView emptyItem in instantiatedEmptyItems)
                emptyItemsPool.Release(emptyItem);

            instantiatedEmptyItems.Clear();
        }

        private static string GetMarketplaceLink(string id)
        {
            const string MARKETPLACE = "https://market.decentraland.org/contracts/{0}/items/{1}";
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

            return string.Format(MARKETPLACE, contract, item);
        }

    }
}
