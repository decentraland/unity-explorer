using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.UI;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteGridController : IDisposable
    {
        private const int CURRENT_PAGE_SIZE = 16;
        private const string EMOTE_CATEGORY = "emote";

        private readonly BackpackGridView view;
        private readonly Button catalogRetry;
        private readonly BackpackCommandBus commandBus;
        private readonly IBackpackEventBus eventBus;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly BackpackSortController backpackSortController;
        private readonly PageSelectorController pageSelectorController;
        private readonly Dictionary<URN, BackpackEmoteGridItemView> usedPoolItems;
        private readonly BackpackEmoteGridItemView?[] loadingResults = new BackpackEmoteGridItemView[CURRENT_PAGE_SIZE];
        private readonly IObjectPool<BackpackEmoteGridItemView> gridItemsPool;
        private readonly IEmoteProvider emoteProvider;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IEmoteStorage emoteStorage;
        private readonly IPendingTransferService ownedNftFilter;

        private CancellationTokenSource? loadElementsCancellationToken;
        private string? currentCategory;
        private string? currentSearch;
        private bool onChainEmotesOnly;
        private int currentPage;
        private IEmoteProvider.OrderOperation currentOrder = new ("date", false);

        public BackpackEmoteGridController(
            EmotesView emotesView,
            BackpackCommandBus commandBus,
            IBackpackEventBus eventBus,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IEquippedEmotes equippedEmotes,
            BackpackSortController backpackSortController,
            PageButtonView pageButtonView,
            IObjectPool<BackpackEmoteGridItemView> gridItemsPool,
            IEmoteProvider emoteProvider,
            IThumbnailProvider thumbnailProvider,
            UnityAppWebBrowser webBrowser,
            IEmoteStorage emoteStorage,
            IPendingTransferService ownedNftFilter)
        {
            view = emotesView.GridView;
            catalogRetry = emotesView.CatalogRetry;
            catalogRetry.onClick.AddListener(RetryPage);
            this.commandBus = commandBus;
            this.eventBus = eventBus;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.equippedEmotes = equippedEmotes;
            this.backpackSortController = backpackSortController;
            this.gridItemsPool = gridItemsPool;
            this.emoteProvider = emoteProvider;
            this.thumbnailProvider = thumbnailProvider;
            this.webBrowser = webBrowser;
            this.emoteStorage = emoteStorage;
            this.ownedNftFilter = ownedNftFilter;
            pageSelectorController = new PageSelectorController(view.PageSelectorView, pageButtonView);
            usedPoolItems = new Dictionary<URN, BackpackEmoteGridItemView>();
            pageSelectorController.OnSetPage += RequestAndFillEmotes;
            eventBus.EquipEmoteEvent += OnEquip;
            eventBus.UnEquipEmoteEvent += OnUnequip;
            view.NoSearchResultsMarketplaceTextLink.OnLinkClicked += OpenMarketplaceLink;
            view.NoCategoryResultsMarketplaceTextLink.OnLinkClicked += OpenMarketplaceLink;
        }

        public void Activate()
        {
            eventBus.FilterEvent += OnFilterEvent;
            backpackSortController.OnSortChanged += OnSortChanged;
            backpackSortController.OnCollectiblesOnlyChanged += OnCollectiblesOnlyChanged;
            RequestAndFillEmotes(1, true);
        }

        public void Deactivate()
        {
            loadElementsCancellationToken.SafeCancelAndDispose();
            eventBus.FilterEvent -= OnFilterEvent;
            backpackSortController.OnSortChanged -= OnSortChanged;
            backpackSortController.OnCollectiblesOnlyChanged -= OnCollectiblesOnlyChanged;
        }

        public void Dispose()
        {
            loadElementsCancellationToken.SafeCancelAndDispose();
            catalogRetry.onClick.RemoveListener(RetryPage);
            pageSelectorController.OnSetPage -= RequestAndFillEmotes;
            eventBus.EquipEmoteEvent -= OnEquip;
            eventBus.UnEquipEmoteEvent -= OnUnequip;
            view.NoSearchResultsMarketplaceTextLink.OnLinkClicked -= OpenMarketplaceLink;
            view.NoCategoryResultsMarketplaceTextLink.OnLinkClicked -= OpenMarketplaceLink;
        }

        public static async UniTask<ObjectPool<BackpackEmoteGridItemView>> InitializeAssetsAsync(IAssetsProvisioner assetsProvisioner,
            BackpackGridView view, CancellationToken ct)
        {
            BackpackEmoteGridItemView backpackItem = (await assetsProvisioner.ProvideMainAssetAsync(view.EmoteGridItem, ct: ct)).Value;

            return new ObjectPool<BackpackEmoteGridItemView>(
                () => Object.Instantiate(backpackItem, view.gameObject.transform),
                defaultCapacity: CURRENT_PAGE_SIZE
            );
        }

        private void RetryPage() =>
            RequestAndFillEmotes(currentPage, currentPage == 1);

        private void RequestAndFillEmotes(int pageNumber)
        {
            RequestAndFillEmotes(pageNumber, false);
        }

        private void RequestAndFillEmotes(int pageNumber, bool reconfigurePageSelector)
        {
            loadElementsCancellationToken = loadElementsCancellationToken.SafeRestart();

            currentPage = pageNumber;
            catalogRetry.gameObject.SetActive(false);
            view.NoSearchResults.SetActive(false);
            view.NoCategoryResults.SetActive(false);
            view.RegularResults.SetActive(true);
            SetGridAsLoading();
            RequestPageAsync(loadElementsCancellationToken.Token).Forget();
            return;

            async UniTaskVoid RequestPageAsync(CancellationToken ct)
            {
                using var ownedScope = ListPool<ITrimmedEmote>.Get(out List<ITrimmedEmote> emotes);

                try
                {
                    var result = await emoteProvider.GetTrimmedByParamsAsync(
                        new IEmoteProvider.OwnedEmotesRequestOptions(
                            pageNum: pageNumber,
                            pageSize: CURRENT_PAGE_SIZE,
                            collectionId: null,
                            orderOperation: currentOrder,
                            name: currentSearch
                        ),
                        ct,
                        emotes
                    ).SuppressToResultAsync(ReportCategory.BACKPACK);

                    if (ct.IsCancellationRequested) return;

                    if (result.Success)
                        ownedNftFilter.Prune(GiftableType.Emote);

                    int totalAmount = result.Success ? result.Value.totalAmount : emotes.Count;

                    bool catalogFailed = !result.Success;
                    if (emotes.Count > CURRENT_PAGE_SIZE)
                        emotes.RemoveRange(CURRENT_PAGE_SIZE, emotes.Count - CURRENT_PAGE_SIZE);

                    if (!onChainEmotesOnly)
                    {
                        using var baseScope = ListPool<IEmote>.Get(out List<IEmote> baseEmotes);
                        IReadOnlyList<URN> pointers = emoteStorage.BaseEmotesUrns;
                        var baseResult = await emoteProvider.GetMetadataByPointersAsync(pointers, ct, baseEmotes)
                                                            .SuppressToResultAsync(ReportCategory.BACKPACK);

                        if (ct.IsCancellationRequested) return;

                        catalogFailed |= !baseResult.Success || (pointers.Count > 0 && baseEmotes.Count == 0);

                        if (!string.IsNullOrEmpty(currentSearch))
                            baseEmotes.RemoveAll(emote => !emote.GetName().Contains(currentSearch, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(currentCategory))
                            baseEmotes.RemoveAll(emote => emote.GetCategory() != currentCategory);

                        if (currentOrder.By == "name")
                            baseEmotes.Sort((left, right) => currentOrder.IsAscending
                                ? string.Compare(left.GetName(), right.GetName(), StringComparison.CurrentCulture)
                                : string.Compare(right.GetName(), left.GetName(), StringComparison.CurrentCulture));

                        // The server pages owned emotes independently. Base emotes start after the last owned item.
                        int firstBaseIndex = Math.Max(0, ((pageNumber - 1) * CURRENT_PAGE_SIZE) - totalAmount);
                        totalAmount += baseEmotes.Count;

                        for (int i = firstBaseIndex; i < baseEmotes.Count && emotes.Count < CURRENT_PAGE_SIZE; i++)
                            emotes.Add(baseEmotes[i]);
                    }

                    if (catalogFailed && emotes.Count == 0)
                    {
                        ShowCatalogError();
                        return;
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

                    SetGridElements(emotes, ct);

                    if (reconfigurePageSelector)
                        pageSelectorController.Configure(totalAmount, CURRENT_PAGE_SIZE);
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    ReportHub.LogException(e, ReportCategory.BACKPACK);

                    if (!ct.IsCancellationRequested)
                        ShowCatalogError();
                }
            }
        }

        private void ShowCatalogError()
        {
            ClearPoolElements();
            view.RegularResults.SetActive(false);
            catalogRetry.gameObject.SetActive(true);
        }

        private void SetGridAsLoading()
        {
            ClearPoolElements();

            for (var i = 0; i < CURRENT_PAGE_SIZE; i++)
            {
                BackpackEmoteGridItemView backpackItemView = gridItemsPool.Get();
                backpackItemView.LoadingView.StartLoadingAnimation(backpackItemView.FullBackpackItem);
                backpackItemView.gameObject.transform.SetAsLastSibling();
                loadingResults[i] = backpackItemView;
            }
        }

        private void SetGridElements(IReadOnlyList<ITrimmedEmote> emotes, CancellationToken ct)
        {
            //Disables and sets the empty slots as first children to avoid the grid to be reorganized
            for (int j = emotes.Count; j < CURRENT_PAGE_SIZE; j++)
            {
                loadingResults[j]!.gameObject.transform.SetAsFirstSibling();
                ReleaseSlot(j);
            }

            for (int i = emotes.Count - 1; i >= 0; i--)
            {
                BackpackEmoteGridItemView backpackItemView = loadingResults[i]!;
                usedPoolItems.Add(emotes[i].GetUrn(), backpackItemView);

                if (emoteStorage.TryGetLatestTransferredAt(emotes[i].GetUrn(), out DateTime latestTransferredAt))
                {
                    TimeSpan timeSinceTransfer = DateTime.UtcNow - latestTransferredAt;
                    backpackItemView.NewTag.SetActive(timeSinceTransfer.TotalHours <= 24);
                }
                else
                {
                    backpackItemView.NewTag.SetActive(false);
                }

                backpackItemView.gameObject.transform.SetAsLastSibling();
                backpackItemView.OnSelectItem += SelectItem;
                backpackItemView.OnEquip += EquipItem;
                backpackItemView.OnUnequip += UnEquipItem;
                backpackItemView.Slot = i;
                backpackItemView.ItemId = emotes[i].GetUrn();
                backpackItemView.RarityBackground.sprite = rarityBackgrounds.GetTypeImage(emotes[i].GetRarity());
                backpackItemView.FlapBackground.color = rarityColors.GetColor(emotes[i].GetRarity());
                backpackItemView.CategoryImage.sprite = categoryIcons.GetTypeImage(EMOTE_CATEGORY);

                int equippedSlot = equippedEmotes.SlotOf(emotes[i]);
                bool isEquipped = equippedSlot != -1;
                backpackItemView.EquippedIcon.SetActive(isEquipped);
                backpackItemView.IsEquipped = isEquipped;
                backpackItemView.IsPending = IsFullyPending(emotes[i].GetUrn());
                backpackItemView.IsCompatibleWithBodyShape = true;
                backpackItemView.EquippedSlotLabel.gameObject.SetActive(isEquipped);
                backpackItemView.EquippedSlotLabel.text = equippedSlot.ToString();

                backpackItemView.SetEquipButtonsState();
                backpackItemView.ShowMetadata(emotes[i].GetName());
                WaitForThumbnailAsync(emotes[i], backpackItemView, ct).Forget();
            }
        }

        // Pending when every owned on-chain instance awaits a gift transfer; off-chain/base emotes have no
        // registry and are never pending.
        private bool IsFullyPending(URN itemUrn) =>
            ownedNftFilter.IsFullyPending(emoteStorage, itemUrn);

        private void UnEquipItem(int slot, string itemId) =>
            commandBus.SendCommand(new BackpackUnEquipEmoteCommand(itemId));

        private void EquipItem(int slot, string itemId) =>
            commandBus.SendCommand(new BackpackEquipEmoteCommand(itemId, null, true, BeginLoadingSlot(slot)));

        private Action BeginLoadingSlot(int slot)
        {
            CancellationToken ct = loadElementsCancellationToken!.Token;
            SetLoadingSlot(slot, true);

            return () =>
            {
                if (!ct.IsCancellationRequested)
                    SetLoadingSlot(slot, false);
            };
        }

        private void SetLoadingSlot(int slot, bool isLoading)
        {
            loadingResults[slot]!.IsLoading = isLoading;

            for (int i = 0; i < loadingResults.Length; i++)
                if (i != slot && loadingResults[i] is { } item)
                    item.CanHover = !isLoading && !item.IsPending;
        }

        private void OnFilterEvent(string? category, AvatarWearableCategoryEnum? categoryEnum, string? searchText)
        {
            currentCategory = string.IsNullOrEmpty(category) ? null : category;
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

        private async UniTaskVoid WaitForThumbnailAsync(ITrimmedEmote emote, BackpackEmoteGridItemView itemView, CancellationToken ct)
        {
            var result = await thumbnailProvider.GetAsync(emote, ct).SuppressToResultAsync(ReportCategory.THUMBNAILS);

            if (result.Success && !ct.IsCancellationRequested)
                itemView.SetThumbnail(result.Value);
        }

        private void ClearPoolElements()
        {
            for (int i = 0; i < loadingResults.Length; i++)
                ReleaseSlot(i);

            usedPoolItems.Clear();
        }

        private void ReleaseSlot(int slot)
        {
            BackpackEmoteGridItemView? item = loadingResults[slot];
            if (item == null) return;

            item.OnEquip -= EquipItem;
            item.OnUnequip -= UnEquipItem;
            item.OnSelectItem -= SelectItem;
            item.ItemId = string.Empty;
            item.IsLoading = false;
            item.CanHover = false;
            item.LoadingView.gameObject.SetActive(false);
            item.FullBackpackItem.SetActive(false);
            gridItemsPool.Release(item);
            loadingResults[slot] = null;
        }

        private void SelectItem(int slot, string itemId) =>
            commandBus.SendCommand(new BackpackSelectEmoteCommand(itemId, BeginLoadingSlot(slot)));

        private void OnUnequip(int slot, IEmote? emote)
        {
            if (emote == null) return;
            if (!usedPoolItems.TryGetValue(emote.GetUrn(), out BackpackEmoteGridItemView backpackItemView)) return;
            backpackItemView.EquippedIcon.SetActive(false);
            backpackItemView.IsEquipped = false;
            backpackItemView.SetEquipButtonsState();
            backpackItemView.EquippedSlotLabel.gameObject.SetActive(false);
        }

        private void OnEquip(int slot, IEmote emote, bool _)
        {
            if (!usedPoolItems.TryGetValue(emote.GetUrn(), out BackpackEmoteGridItemView backpackItemView)) return;
            backpackItemView.IsEquipped = true;
            backpackItemView.SetEquipButtonsState();
            backpackItemView.EquippedSlotLabel.gameObject.SetActive(true);
            backpackItemView.EquippedSlotLabel.text = slot.ToString();
        }

        private void OpenMarketplaceLink(string url) =>
            webBrowser.OpenUrlMainThreadOnly(url);
    }
}
