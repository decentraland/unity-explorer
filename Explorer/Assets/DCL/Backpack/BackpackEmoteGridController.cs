using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Breadcrumb;
using DCL.UI;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Backpack
{
    public class BackpackEmoteGridController : IDisposable
    {
        private const int CURRENT_PAGE_SIZE = 16;
        private const string EMOTE_CATEGORY = "emote";

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
        private readonly IReadOnlyCollection<URN> embeddedEmoteIds;

        private CancellationTokenSource? loadElementsCancellationToken;
        private string? currentCategory;
        private string? currentSearch;
        private bool onChainEmotesOnly;
        private IEmoteProvider.OrderOperation currentOrder = new ("date", false);
        private BodyShape currentBodyShape = BodyShape.MALE;

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
            IEmoteProvider emoteProvider,
            IReadOnlyCollection<URN> embeddedEmoteIds)
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
            this.embeddedEmoteIds = embeddedEmoteIds;
            pageSelectorController = new PageSelectorController(view.PageSelectorView, pageButtonView);

            usedPoolItems = new Dictionary<URN, BackpackItemView>();
            eventBus.EquipEmoteEvent += OnEquip;
            eventBus.EquipWearableEvent += OnWearableEquipped;
            eventBus.UnEquipEmoteEvent += OnUnequip;
            eventBus.FilterCategoryEvent += OnFilterCategory;
            eventBus.SearchEvent += OnSearch;
            backpackSortController.OnSortChanged += OnSortChanged;
            backpackSortController.OnCollectiblesOnlyChanged += OnCollectiblesOnlyChanged;
            pageSelectorController.OnSetPage += RequestAndFillEmotes;
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

        private void RequestAndFillEmotes(int pageNumber)
        {
            RequestAndFillEmotes(pageNumber, false);
        }

        public void RequestAndFillEmotes(int pageNumber, bool reconfigurePageSelector)
        {
            loadElementsCancellationToken = loadElementsCancellationToken.SafeRestart();

            SetGridAsLoading();

            async UniTaskVoid RequestPageAsync(CancellationToken ct)
            {
                IReadOnlyList<IEmote> emotes;

                (IReadOnlyList<IEmote>? customOwnedEmotes, int totalAmount) = await emoteProvider.GetOwnedEmotesAsync(web3IdentityCache.Identity!.Address,
                    pageNum: pageNumber, pageSize: CURRENT_PAGE_SIZE,
                    orderOperation: currentOrder,
                    name: currentSearch,
                    ct: ct);

                if (onChainEmotesOnly)
                    emotes = customOwnedEmotes;
                else
                {
                    IReadOnlyList<IEmote> embeddedEmotes = await emoteProvider.GetEmotesAsync(embeddedEmoteIds, BodyShape.MALE, ct);
                    totalAmount += embeddedEmotes.Count;

                    emotes = customOwnedEmotes
                            .Concat(embeddedEmotes)
                            .Skip((pageNumber - 1) * CURRENT_PAGE_SIZE)
                            .Take(CURRENT_PAGE_SIZE)
                            .ToArray();
                }

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

                if(reconfigurePageSelector)
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

        private void SetGridElements(IReadOnlyList<IEmote> emotes)
        {
            //Disables and sets the empty slots as first children to avoid the grid to be reorganized
            for (int j = emotes.Count; j < CURRENT_PAGE_SIZE; j++)
            {
                loadingResults[j]!.gameObject.transform.SetAsFirstSibling();
                loadingResults[j]!.LoadingView.gameObject.SetActive(false);
                loadingResults[j]!.FullBackpackItem.SetActive(false);
                usedPoolItems.Remove(j);

                if (loadingResults[j] != null)
                    gridItemsPool.Release(loadingResults[j]!);
            }

            for (int i = emotes.Count - 1; i >= 0; i--)
            {
                BackpackItemView backpackItemView = loadingResults[i]!;
                usedPoolItems.Remove(i);
                usedPoolItems.Add(emotes[i].GetUrn(), backpackItemView);
                backpackItemView.gameObject.transform.SetAsLastSibling();
                backpackItemView.OnSelectItem += SelectItem;
                backpackItemView.EquipButton.onClick.AddListener(() => commandBus.SendCommand(new BackpackEquipWearableCommand(backpackItemView.ItemId)));
                backpackItemView.UnEquipButton.onClick.AddListener(() => commandBus.SendCommand(new BackpackUnEquipWearableCommand(backpackItemView.ItemId)));
                backpackItemView.ItemId = emotes[i].GetUrn();
                backpackItemView.RarityBackground.sprite = rarityBackgrounds.GetTypeImage(emotes[i].GetRarity());
                backpackItemView.FlapBackground.color = rarityColors.GetColor(emotes[i].GetRarity());
                backpackItemView.CategoryImage.sprite = categoryIcons.GetTypeImage(EMOTE_CATEGORY);
                backpackItemView.EquippedIcon.SetActive(backpackEquipStatusController.IsEmoteEquipped(emotes[i]));
                backpackItemView.IsEquipped = backpackEquipStatusController.IsEmoteEquipped(emotes[i]);

                backpackItemView.SetEquipButtonsState();
                WaitForThumbnailAsync(emotes[i], backpackItemView, loadElementsCancellationToken!.Token).Forget();
            }
        }

        private void OnFilterCategory(string category)
        {
            currentCategory = string.IsNullOrEmpty(category) ? null : category;
            RequestAndFillEmotes(1, true);
        }

        private void OnSearch(string searchText)
        {
            currentSearch = string.IsNullOrEmpty(searchText) ? null : searchText;
            RequestAndFillEmotes(1, true);
        }

        private void OnSortChanged(BackpackGridSort sort)
        {
            string by = sort.OrderByOperation.ToString().ToLower();
            currentOrder = new IEmoteProvider.OrderOperation(by, sort.SortAscending);
            RequestAndFillEmotes(1, true);
        }

        private void OnCollectiblesOnlyChanged(bool collectiblesOnly)
        {
            onChainEmotesOnly = collectiblesOnly;
            RequestAndFillEmotes(1, true);
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

        private void OnWearableEquipped(IWearable wearable)
        {
            if (wearable.GetCategory() != WearablesConstants.Categories.BODY_SHAPE) return;

            foreach (BodyShape bodyShape in BodyShape.VALUES)
            {
                if (wearable.GetUrn() != bodyShape) continue;
                currentBodyShape = bodyShape;
                return;
            }
        }
    }
}
