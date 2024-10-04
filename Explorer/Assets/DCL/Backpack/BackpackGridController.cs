using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Breadcrumb;
using DCL.Diagnostics;
using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;
using Object = UnityEngine.Object;

namespace DCL.Backpack
{
    public class BackpackGridController
    {
        private const int CURRENT_PAGE_SIZE = 16;

        private readonly BackpackGridView view;
        private readonly BackpackCommandBus commandBus;
        private readonly IBackpackEventBus eventBus;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IReadOnlyEquippedWearables equippedWearables;
        private readonly BackpackSortController backpackSortController;
        private readonly PageSelectorController pageSelectorController;
        private readonly Dictionary<URN, BackpackItemView> usedPoolItems;
        private readonly List<IWearable> results = new (CURRENT_PAGE_SIZE);
        private readonly BackpackItemView?[] loadingResults = new BackpackItemView[CURRENT_PAGE_SIZE];
        private readonly IObjectPool<BackpackItemView> gridItemsPool;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IWearablesProvider wearablesProvider;

        private CancellationTokenSource? pageFetchCancellationToken;
        private bool currentCollectiblesOnly;
        private string currentCategory = "";
        private string currentSearch = "";
        private BackpackGridSort currentSort = new (NftOrderByOperation.Date, false);
        private IWearable? currentBodyShape;
        private IReadOnlyList<IWearable>? currentPageWearables;

        public BackpackGridController(
            BackpackGridView view,
            BackpackCommandBus commandBus,
            IBackpackEventBus eventBus,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IReadOnlyEquippedWearables equippedWearables,
            BackpackSortController backpackSortController,
            PageButtonView pageButtonView,
            IObjectPool<BackpackItemView> gridItemsPool,
            IThumbnailProvider thumbnailProvider,
            ColorToggleView colorToggle,
            ColorPresetsSO hairColors,
            ColorPresetsSO eyesColors,
            ColorPresetsSO bodyshapeColors,
            IWearablesProvider wearablesProvider)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.eventBus = eventBus;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.equippedWearables = equippedWearables;
            this.backpackSortController = backpackSortController;
            this.thumbnailProvider = thumbnailProvider;
            this.wearablesProvider = wearablesProvider;
            this.gridItemsPool = gridItemsPool;
            pageSelectorController = new PageSelectorController(view.PageSelectorView, pageButtonView);

            usedPoolItems = new Dictionary<URN, BackpackItemView>();
            pageSelectorController.OnSetPage += (int page) => RequestPage(page, false);
            new BackpackBreadCrumbController(view.BreadCrumbView, eventBus, commandBus, categoryIcons, colorToggle, hairColors, eyesColors, bodyshapeColors);
            eventBus.EquipWearableEvent += OnEquip;
            eventBus.UnEquipWearableEvent += OnUnequip;
        }

        public void Activate()
        {
            eventBus.FilterCategoryEvent += OnFilterCategory;
            eventBus.SearchEvent += OnSearch;
            backpackSortController.OnSortChanged += OnSortChanged;
            backpackSortController.OnCollectiblesOnlyChanged += OnCollectiblesOnlyChanged;
        }

        public void Deactivate()
        {
            eventBus.FilterCategoryEvent -= OnFilterCategory;
            eventBus.SearchEvent -= OnSearch;
            backpackSortController.OnSortChanged -= OnSortChanged;
            backpackSortController.OnCollectiblesOnlyChanged -= OnCollectiblesOnlyChanged;
        }

        public static async UniTask<ObjectPool<BackpackItemView>> InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, BackpackGridView view, CancellationToken ct)
        {
            BackpackItemView backpackItem = (await assetsProvisioner.ProvideMainAssetAsync(view.BackpackItem, ct: ct)).Value;

            return new ObjectPool<BackpackItemView>(
                () => CreateBackpackItem(backpackItem),
                defaultCapacity: CURRENT_PAGE_SIZE
            );

            BackpackItemView CreateBackpackItem(BackpackItemView backpackItem)
            {
                BackpackItemView backpackItemView = Object.Instantiate(backpackItem, view.gameObject.transform);
                return backpackItemView;
            }
        }

        private void SetGridAsLoading()
        {
            ClearPoolElements();

            for (var i = 0; i < CURRENT_PAGE_SIZE; i++)
            {
                BackpackItemView backpackItemView = gridItemsPool.Get();
                backpackItemView.LoadingView.StartLoadingAnimation(backpackItemView.FullBackpackItem);
                backpackItemView.gameObject.transform.SetAsLastSibling();
                loadingResults[i] = backpackItemView;
                usedPoolItems.Add(i.ToString(), backpackItemView);
            }
        }

        private void SetGridElements(IReadOnlyList<IWearable> gridWearables)
        {
            //Disables and sets the empty slots as first children to avoid the grid to be reorganized
            for (int j = gridWearables.Count; j < CURRENT_PAGE_SIZE; j++)
            {
                loadingResults[j].gameObject.transform.SetAsFirstSibling();
                loadingResults[j].LoadingView.gameObject.SetActive(false);
                loadingResults[j].FullBackpackItem.SetActive(false);
                usedPoolItems.Remove(j);
                gridItemsPool.Release(loadingResults[j]);
            }

            for (int i = Math.Min(gridWearables.Count, loadingResults.Length) - 1; i >= 0; i--)
            {
                //This only happens in last page of results, when gridWearables returned twice the amount of wearables
                //caused by clicking repeatedly on the same number on the backpack
                if (usedPoolItems.ContainsKey(gridWearables[i].GetUrn()))
                    continue;

                BackpackItemView backpackItemView = loadingResults[i];
                usedPoolItems.Remove(i);
                usedPoolItems.Add(gridWearables[i].GetUrn(), backpackItemView);
                backpackItemView.gameObject.transform.SetAsLastSibling();
                backpackItemView.OnEquip += EquipItem;
                backpackItemView.OnSelectItem += SelectItem;
                backpackItemView.OnUnequip += UnEquipItem;
                backpackItemView.ItemId = gridWearables[i].GetUrn();
                backpackItemView.RarityBackground.sprite = rarityBackgrounds.GetTypeImage(gridWearables[i].GetRarity());
                backpackItemView.FlapBackground.color = rarityColors.GetColor(gridWearables[i].GetRarity());
                backpackItemView.CategoryImage.sprite = categoryIcons.GetTypeImage(gridWearables[i].GetCategory());
                backpackItemView.EquippedIcon.SetActive(equippedWearables.IsEquipped(gridWearables[i]));
                backpackItemView.IsEquipped = equippedWearables.IsEquipped(gridWearables[i]);

                backpackItemView.IsCompatibleWithBodyShape = (currentBodyShape != null
                                                              && gridWearables[i].IsCompatibleWithBodyShape(currentBodyShape.GetUrn()))
                                                             || gridWearables[i].GetCategory() == WearablesConstants.Categories.BODY_SHAPE;

                backpackItemView.SetEquipButtonsState();
                WaitForThumbnailAsync(gridWearables[i], backpackItemView, pageFetchCancellationToken!.Token).Forget();
            }
        }

        private void EquipItem(string itemId) =>
            commandBus.SendCommand(new BackpackEquipWearableCommand(itemId));

        private void UnEquipItem(string itemId) =>
            commandBus.SendCommand(new BackpackUnEquipWearableCommand(itemId));

        private void OnFilterCategory(string category)
        {
            if (currentCategory == category)
                return;

            currentCategory = category;
            RequestPage(1, true);
        }

        private void OnSearch(string searchText)
        {
            if (currentSearch == searchText)
                return;

            currentSearch = searchText;
            RequestPage(1, true);
        }

        private void OnSortChanged(BackpackGridSort sort)
        {
            currentSort = sort;
            RequestPage(1, true);
        }

        private void OnCollectiblesOnlyChanged(bool collectiblesOnly)
        {
            currentCollectiblesOnly = collectiblesOnly;
            RequestPage(1, true);
        }

        public void RequestPage(int pageNumber, bool refreshPageSelector)
        {
            pageFetchCancellationToken = pageFetchCancellationToken.SafeRestart();
            SetGridAsLoading();
            AwaitWearablesPromiseAsync(pageNumber, refreshPageSelector, pageFetchCancellationToken.Token).Forget();
        }

        private async UniTaskVoid AwaitWearablesPromiseAsync(int pageNumber, bool refreshPageSelector, CancellationToken ct)
        {
            if (refreshPageSelector)
                pageSelectorController.SetActive(false);

            IWearablesProvider.CollectionType collectionType = IWearablesProvider.CollectionType.All;

            if (currentCollectiblesOnly)
                collectionType = IWearablesProvider.CollectionType.OnChain | IWearablesProvider.CollectionType.ThirdParty;

            results.Clear();

            try
            {
                (IReadOnlyList<IWearable>? wearables, int totalAmount) = await wearablesProvider.GetAsync(CURRENT_PAGE_SIZE, pageNumber, ct,
                    currentSort.OrderByOperation.ToSortingField(),
                    currentSort.SortAscending ? IWearablesProvider.OrderBy.Ascending : IWearablesProvider.OrderBy.Descending,
                    currentCategory, collectionType, currentSearch, results);

                if (refreshPageSelector)
                    pageSelectorController.Configure(totalAmount, CURRENT_PAGE_SIZE);

                currentPageWearables = wearables;

                if (currentPageWearables.Count == 0)
                {
                    view.NoSearchResults.SetActive(!string.IsNullOrEmpty(currentSearch));
                    view.NoCategoryResults.SetActive(!string.IsNullOrEmpty(currentCategory));
                    view.RegularResults.SetActive(string.IsNullOrEmpty(currentSearch) && string.IsNullOrEmpty(currentCategory));
                }
                else
                {
                    view.NoSearchResults.SetActive(false);
                    view.NoCategoryResults.SetActive(false);
                    view.RegularResults.SetActive(true);
                }

                SetGridElements(currentPageWearables);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.BACKPACK));
            }
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable itemWearable, BackpackItemView itemView, CancellationToken ct)
        {
            Sprite sprite = await thumbnailProvider.GetAsync(itemWearable, ct);

            if (ct.IsCancellationRequested) return;

            itemView.WearableThumbnail.sprite = sprite;
            itemView.LoadingView.FinishLoadingAnimation(itemView.FullBackpackItem);
        }

        private void ClearPoolElements()
        {
            foreach (KeyValuePair<URN, BackpackItemView> backpackItemView in usedPoolItems)
            {
                backpackItemView.Value.OnUnequip -= UnEquipItem;
                backpackItemView.Value.OnEquip -= EquipItem;
                backpackItemView.Value.OnSelectItem -= SelectItem;
                backpackItemView.Value.EquippedIcon.SetActive(false);
                backpackItemView.Value.IsEquipped = false;
                backpackItemView.Value.IsCompatibleWithBodyShape = true;
                backpackItemView.Value.ItemId = "";
                gridItemsPool.Release(backpackItemView.Value);
            }

            for (var i = 0; i < loadingResults.Length; i++)
                loadingResults[i] = null;

            usedPoolItems.Clear();
        }

        private void SelectItem(string itemId) =>
            commandBus.SendCommand(new BackpackSelectWearableCommand(itemId));

        private void OnUnequip(IWearable unequippedWearable)
        {
            if (usedPoolItems.TryGetValue(unequippedWearable.GetUrn(), out BackpackItemView backpackItemView))
            {
                backpackItemView.EquippedIcon.SetActive(false);
                backpackItemView.IsEquipped = false;
                backpackItemView.SetEquipButtonsState();
            }
        }

        private void OnEquip(IWearable equippedWearable)
        {
            if (usedPoolItems.TryGetValue(equippedWearable.GetUrn(), out BackpackItemView backpackItemView))
            {
                backpackItemView.IsEquipped = true;
                backpackItemView.SetEquipButtonsState();
            }

            if (equippedWearable.GetCategory() == WearablesConstants.Categories.BODY_SHAPE)
            {
                currentBodyShape = equippedWearable;

                // Forces to re-set body shape compatibility to items
                if (currentPageWearables != null)
                    UpdateBodyShapeCompatibility(currentPageWearables, currentBodyShape);
            }
        }

        private void UpdateBodyShapeCompatibility(IReadOnlyList<IWearable> wearables, IAvatarAttachment bodyShape)
        {
            for (int i = Math.Min(wearables.Count, loadingResults.Length) - 1; i >= 0; i--)
            {
                IWearable wearable = wearables[i];
                BackpackItemView? itemView = loadingResults[i];

                if (itemView == null) continue;

                itemView.IsCompatibleWithBodyShape = wearable.IsCompatibleWithBodyShape(bodyShape.GetUrn())
                                                     || wearable.GetCategory() == WearablesConstants.Categories.BODY_SHAPE;

                itemView.SetEquipButtonsState();
            }
        }
    }
}
