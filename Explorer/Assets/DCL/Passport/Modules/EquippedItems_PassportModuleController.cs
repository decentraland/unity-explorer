using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Passport.Fields;
using DCL.Profiles;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;

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
        private readonly IObjectPool<EquippedItem_PassportFieldView> loadingItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedLoadingItems = new();
        private readonly IObjectPool<EquippedItem_PassportFieldView> equippedItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedEquippedItems = new();
        private readonly IObjectPool<EquippedItem_PassportFieldView> emptyItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedEmptyItems = new();

        private Profile currentProfile;
        private CancellationTokenSource cts;

        public EquippedItems_PassportModuleController(
            EquippedItems_PassportModuleView view,
            World world,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IThumbnailProvider thumbnailProvider,
            RectTransform scrollContainer)
        {
            this.view = view;
            this.world = world;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.thumbnailProvider = thumbnailProvider;
            this.scrollContainer = scrollContainer;

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
                actionOnRelease: equippedItemView => equippedItemView.gameObject.SetActive(false));

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

        public void Dispose() =>
            Clear();

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

            AwaitEquippedItemsPromiseAsync(equippedWearablesPromise, equippedEmotesPromise, cts.Token).Forget();
        }

        private void SetGridAsLoading()
        {
            cts = cts.SafeRestart();

            for (var i = 0; i < LOADING_ITEMS_POOL_DEFAULT_CAPACITY; i++)
            {
                var loadingItem = loadingItemsPool.Get();
                loadingItem.gameObject.name = "LoadingItem";
                instantiatedLoadingItems.Add(loadingItem);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.EquippedItemsContainer);
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
                equippedWearableItem.BuyButton.interactable = wearable.IsCollectible();
                LayoutRebuilder.ForceRebuildLayoutImmediate(equippedWearableItem.RarityLabelContainer);
                WaitForThumbnailAsync(wearable, equippedWearableItem, cts.Token).Forget();
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
                equippedWearableItem.BuyButton.interactable = emote.IsCollectible() && rarityName != "base";
                LayoutRebuilder.ForceRebuildLayoutImmediate(equippedWearableItem.RarityLabelContainer);
                WaitForThumbnailAsync(emote, equippedWearableItem, cts.Token).Forget();
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

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.EquippedItemsContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContainer);
        }

        private int CalculateMissingEmptyItems(int totalItems)
        {
            int remainder = totalItems % 6;
            int missingItems = (remainder == 0) ? 0 : 6 - remainder;
            return missingItems;
        }

        private async UniTaskVoid AwaitEquippedItemsPromiseAsync(WearablePromise equippedWearablesPromise, EmotePromise equippedEmotesPromise, CancellationToken ct)
        {
            var wearablesUniTaskAsync = await equippedWearablesPromise.ToUniTaskAsync(world, cancellationToken: ct);
            var emotesUniTaskAsync = await equippedEmotesPromise.ToUniTaskAsync(world, cancellationToken: ct);

            if (!wearablesUniTaskAsync.Result!.Value.Succeeded || !emotesUniTaskAsync.Result!.Value.Succeeded || ct.IsCancellationRequested)
                return;

            var currentWearables = wearablesUniTaskAsync.Result.Value.Asset.Wearables;
            var currentEmotes = emotesUniTaskAsync.Result.Value.Asset.Emotes;
            SetGridElements(currentWearables, currentEmotes);
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable itemWearable, EquippedItem_PassportFieldView itemView, CancellationToken ct)
        {
            Sprite? sprite = await thumbnailProvider.GetAsync(itemWearable, ct);

            if (ct.IsCancellationRequested)
                return;

            itemView.EquippedItemThumbnail.sprite = sprite;
        }

        private async UniTaskVoid WaitForThumbnailAsync(IEmote itemEmote, EquippedItem_PassportFieldView itemView, CancellationToken ct)
        {
            Sprite? sprite = await thumbnailProvider.GetAsync(itemEmote, ct);

            if (ct.IsCancellationRequested)
                return;

            itemView.EquippedItemThumbnail.sprite = sprite;
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
    }
}
