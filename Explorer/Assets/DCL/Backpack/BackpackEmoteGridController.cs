using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Breadcrumb;
using DCL.UI;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Backpack
{
    public class BackpackEmoteGridController : IDisposable
    {
        private const int CURRENT_PAGE_SIZE = 16;

        private readonly BackpackGridView view;
        private readonly BackpackCommandBus commandBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IBackpackEquipStatusController backpackEquipStatusController;

        private readonly PageSelectorController pageSelectorController;
        private readonly Dictionary<URN, BackpackItemView> usedPoolItems;
        private readonly BackpackItemView?[] loadingResults = new BackpackItemView[CURRENT_PAGE_SIZE];
        private readonly IObjectPool<BackpackItemView> gridItemsPool;
        private readonly IEmoteProvider emoteProvider;

        private CancellationTokenSource? loadElementsCancellationToken;
        private string? currentCategory;
        private string? currentSearch;
        private bool onChainEmotesOnly;
        private IEmoteProvider.OrderOperation currentOrder = new ("date", false);
        private BackpackBreadCrumbController breadCrumbController;

        public BackpackEmoteGridController(
            BackpackGridView view,
            BackpackCommandBus commandBus,
            BackpackEventBus eventBus,
            IWeb3IdentityCache web3IdentityCache,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IBackpackEquipStatusController backpackEquipStatusController,
            BackpackSortController backpackSortController,
            PageButtonView pageButtonView,
            IObjectPool<BackpackItemView> gridItemsPool,
            IEmoteProvider emoteProvider)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.web3IdentityCache = web3IdentityCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.backpackEquipStatusController = backpackEquipStatusController;
            this.gridItemsPool = gridItemsPool;
            this.emoteProvider = emoteProvider;
            pageSelectorController = new PageSelectorController(view.PageSelectorView, pageButtonView);

            usedPoolItems = new Dictionary<URN, BackpackItemView>();
            eventBus.EquipEmoteEvent += OnEquip;
            eventBus.UnEquipEmoteEvent += OnUnequip;
            eventBus.FilterCategoryEvent += OnFilterCategory;
            eventBus.SearchEvent += OnSearch;
            backpackSortController.OnSortChanged += OnSortChanged;
            backpackSortController.OnCollectiblesOnlyChanged += OnCollectiblesOnlyChanged;
            pageSelectorController.OnSetPage += RequestAndFillEmotes;
            breadCrumbController = new BackpackBreadCrumbController(view.BreadCrumbView, eventBus, commandBus, categoryIcons);
        }

        public void Dispose()
        {
            loadElementsCancellationToken.SafeCancelAndDispose();
        }

        public static async UniTask<ObjectPool<BackpackItemView>> InitializeAssetsAsync(IAssetsProvisioner assetsProvisioner,
            BackpackGridView view, CancellationToken ct)
        {
            BackpackItemView backpackItem = (await assetsProvisioner.ProvideMainAssetAsync(view.BackpackItem, ct: ct)).Value;

            return new ObjectPool<BackpackItemView>(
                () => Object.Instantiate(backpackItem, view.gameObject.transform),
                defaultCapacity: CURRENT_PAGE_SIZE
            );
        }

        public void RequestAndFillEmotes(int pageNumber)
        {
            loadElementsCancellationToken = loadElementsCancellationToken.SafeRestart();

            SetGridAsLoading();

            async UniTaskVoid RequestPageAsync(CancellationToken ct)
            {
                (IReadOnlyList<IEmote>? emotes, int totalAmount) = await emoteProvider.GetOwnedEmotesAsync(web3IdentityCache.Identity!.Address,
                    pageNum: pageNumber, pageSize: CURRENT_PAGE_SIZE,
                    orderOperation: currentOrder,
                    name: currentSearch,
                    onChainCollectionsOnly: onChainEmotesOnly,
                    ct: ct);

                if (emotes.Count == 0)
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

                SetGridElements(emotes);
                pageSelectorController.Configure(totalAmount, CURRENT_PAGE_SIZE);
            }

            RequestPageAsync(loadElementsCancellationToken!.Token).Forget();
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

        private void SetGridElements(IReadOnlyList<IEmote> gridWearables)
        {
            //Disables and sets the empty slots as first children to avoid the grid to be reorganized
            for (int j = gridWearables.Count; j < CURRENT_PAGE_SIZE; j++)
            {
                loadingResults[j]!.gameObject.transform.SetAsFirstSibling();
                loadingResults[j]!.LoadingView.gameObject.SetActive(false);
                loadingResults[j]!.FullBackpackItem.SetActive(false);
                usedPoolItems.Remove(j);

                if (loadingResults[j] != null)
                    gridItemsPool.Release(loadingResults[j]!);
            }

            for (int i = gridWearables.Count - 1; i >= 0; i--)
            {
                BackpackItemView backpackItemView = loadingResults[i]!;
                usedPoolItems.Remove(i);
                usedPoolItems.Add(gridWearables[i].GetUrn(), backpackItemView);
                backpackItemView.gameObject.transform.SetAsLastSibling();
                backpackItemView.OnSelectItem += SelectItem;
                backpackItemView.EquipButton.onClick.AddListener(() => commandBus.SendCommand(new BackpackEquipWearableCommand(backpackItemView.ItemId)));
                backpackItemView.UnEquipButton.onClick.AddListener(() => commandBus.SendCommand(new BackpackUnEquipWearableCommand(backpackItemView.ItemId)));
                backpackItemView.ItemId = gridWearables[i].GetUrn();
                backpackItemView.RarityBackground.sprite = rarityBackgrounds.GetTypeImage(gridWearables[i].GetRarity());
                backpackItemView.FlapBackground.color = rarityColors.GetColor(gridWearables[i].GetRarity());
                backpackItemView.CategoryImage.sprite = categoryIcons.GetTypeImage(gridWearables[i].GetCategory());
                backpackItemView.EquippedIcon.SetActive(backpackEquipStatusController.IsEmoteEquipped(gridWearables[i]));
                backpackItemView.IsEquipped = backpackEquipStatusController.IsEmoteEquipped(gridWearables[i]);

                backpackItemView.SetEquipButtonsState();
                WaitForThumbnailAsync(gridWearables[i], backpackItemView, loadElementsCancellationToken.Token).Forget();
            }
        }

        private void OnFilterCategory(string category)
        {
            currentCategory = string.IsNullOrEmpty(category) ? null : category;
            RequestAndFillEmotes(1);
        }

        private void OnSearch(string searchText)
        {
            currentSearch = string.IsNullOrEmpty(searchText) ? null : searchText;
            RequestAndFillEmotes(1);
        }

        private void OnSortChanged(BackpackGridSort sort)
        {
            string by = sort.OrderByOperation.ToString().ToLower();
            currentOrder = new IEmoteProvider.OrderOperation(by, sort.SortAscending);
            RequestAndFillEmotes(1);
        }

        private void OnCollectiblesOnlyChanged(bool collectiblesOnly)
        {
            onChainEmotesOnly = collectiblesOnly;
            RequestAndFillEmotes(1);
        }

        private async UniTaskVoid WaitForThumbnailAsync(IAvatarAttachment emote, BackpackItemView itemView, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            do { await UniTask.Delay(250, cancellationToken: ct); }
            while (emote.ThumbnailAssetResult == null || !emote.ThumbnailAssetResult.HasValue);

            itemView.WearableThumbnail.sprite = emote.ThumbnailAssetResult.Value.Asset;
            itemView.LoadingView.FinishLoadingAnimation(itemView.FullBackpackItem);
        }

        private void ClearPoolElements()
        {
            foreach (KeyValuePair<URN, BackpackItemView> backpackItemView in usedPoolItems)
            {
                backpackItemView.Value.EquipButton.onClick.RemoveAllListeners();
                backpackItemView.Value.UnEquipButton.onClick.RemoveAllListeners();
                backpackItemView.Value.OnSelectItem -= SelectItem;
                backpackItemView.Value.EquippedIcon.SetActive(false);
                backpackItemView.Value.IsEquipped = false;
                backpackItemView.Value.ItemId = "";
                gridItemsPool.Release(backpackItemView.Value);
            }

            for (var i = 0; i < loadingResults.Length; i++)
                loadingResults[i] = null;

            usedPoolItems.Clear();
        }

        private void SelectItem(string itemId) =>
            commandBus.SendCommand(new BackpackSelectEmoteCommand(itemId));

        private void OnUnequip(int slot, IEmote? emote)
        {
            if (emote == null) return;
            if (!usedPoolItems.TryGetValue(emote.GetUrn(), out BackpackItemView backpackItemView)) return;
            backpackItemView.EquippedIcon.SetActive(false);
            backpackItemView.IsEquipped = false;
            backpackItemView.SetEquipButtonsState();
        }

        private void OnEquip(int slot, IEmote emote)
        {
            if (!usedPoolItems.TryGetValue(emote.GetUrn(), out BackpackItemView backpackItemView)) return;
            backpackItemView.IsEquipped = true;
            backpackItemView.SetEquipButtonsState();
        }
    }
}
