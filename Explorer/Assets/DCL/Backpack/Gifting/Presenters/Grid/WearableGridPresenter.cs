using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.Gifting.Cache;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class WearableGridPresenter : IGiftingGridPresenter<WearableViewModel>
    {
        private const int CURRENT_PAGE_SIZE = 16;
        private const int SEARCH_DEBOUNCE_MS = 500;
        public event Action<string?>? OnSelectionChanged;
        public int CurrentItemCount => viewModelsByUrn.Count;
        public event Action<bool> OnLoadingStateChanged;
        public string? SelectedUrn { get; set; }
        public int ItemCount => viewModelUrnOrder.Count;

        private readonly GiftingGridView view;
        private readonly SuperScrollGridAdapter<WearableViewModel> adapter;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEventBus eventBus;
        private readonly LoadGiftableItemThumbnailCommand loadThumbnailCommand;
        private readonly IWearableStylingCatalog wearableStylingCatalog;
        private readonly IReadOnlyEquippedWearables equippedWearables;
        private EquippedItemContext equippedContext;

        private readonly List<IWearable> results = new (CURRENT_PAGE_SIZE);
        private readonly Dictionary<string, WearableViewModel> viewModelsByUrn = new();
        private readonly List<string> viewModelUrnOrder = new();
        private readonly RectTransform rectTransform;
        private readonly CanvasGroup canvasGroup;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? fetchCts;
        private CancellationTokenSource? searchCts;
        private IDisposable? thumbnailLoadedSubscription;

        private int currentPage;
        private int totalCount = int.MaxValue;
        private string currentSearch = string.Empty;
        private bool isLoading;
        private bool canLoadNextPage = true;
        private int pendingThumbnailLoads  ;
        private readonly BackpackGridSort currentSort = new (NftOrderByOperation.Date, false);

        public WearableGridPresenter(GiftingGridView view,
            SuperScrollGridAdapter<WearableViewModel> adapter,
            IWearablesProvider wearablesProvider,
            IEventBus eventBus,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand,
            IWearableStylingCatalog wearableStylingCatalog,
            IReadOnlyEquippedWearables equippedWearables)
        {
            this.view = view;
            this.adapter = adapter;
            this.wearablesProvider = wearablesProvider;
            this.eventBus = eventBus;
            this.loadThumbnailCommand = loadThumbnailCommand;
            this.wearableStylingCatalog  = wearableStylingCatalog;
            this.equippedWearables = equippedWearables;
            
            rectTransform = view.GetComponent<RectTransform>();
            canvasGroup = view.GetComponent<CanvasGroup>();

            adapter.UseWearableStyling(wearableStylingCatalog);
            adapter.SetDataProvider(this);
        }

        public void PrepareForLoading(EquippedItemContext context)
        {
            equippedContext = context;
        }

        public void Activate()
        {
            lifeCts = new CancellationTokenSource();

            thumbnailLoadedSubscription = eventBus
                .Subscribe<GiftingEvents.ThumbnailLoadedEvent>(OnThumbnailLoaded);

            adapter.OnNearEndOfScroll += OnNearEndOfScroll;
            adapter.OnItemSelected += OnItemSelected;

            ClearData();
            adapter.RefreshData();
            RequestNextPage().Forget();
        }

        public void Deactivate()
        {
            thumbnailLoadedSubscription?.Dispose();
            adapter.OnNearEndOfScroll -= OnNearEndOfScroll;
            adapter.OnItemSelected -= OnItemSelected;

            searchCts.SafeCancelAndDispose();
            fetchCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();

            HardClear();
        }

        private void HardClear()
        {
            ClearData();
            results.Clear();

            adapter.RefreshData();
            adapter.RefreshAllShownItem();
        }

        private void OnAllLoadsFinished()
        {
            if (lifeCts?.IsCancellationRequested == true) return;
            
            canLoadNextPage = true;
            if (adapter.IsNearEnd && !isLoading && ItemCount < totalCount)
                RequestNextPage().Forget();
        }

        public void SetSearchText(string searchText)
        {
            searchText ??= string.Empty;
            if (currentSearch == searchText) return;
            currentSearch = searchText;

            searchCts = searchCts.SafeRestartLinked(lifeCts.Token);
            DebouncedSearchAsync(searchCts.Token).Forget();
        }

        private async UniTaskVoid DebouncedSearchAsync(CancellationToken ct)
        {
            await UniTask.Delay(SEARCH_DEBOUNCE_MS, cancellationToken: ct);
            if (ct.IsCancellationRequested) return;

            fetchCts
                .SafeCancelAndDispose();

            ClearData();

            adapter
                .RefreshData();

            RequestNextPage()
                .Forget();
        }

        private void OnNearEndOfScroll()
        {
            if (canLoadNextPage && !isLoading && ItemCount < totalCount)
                RequestNextPage().Forget();
        }

        private void OnItemSelected(string urn)
        {
            if (viewModelsByUrn.TryGetValue(urn, out var viewModel) && !viewModel.IsGiftable)
                return;

            SelectedUrn = SelectedUrn == urn ? null : urn;
            OnSelectionChanged?.Invoke(SelectedUrn);
            adapter.RefreshAllShownItem();
        }

        private async UniTask RequestNextPage()
        {
            if (isLoading) return;

            isLoading = true;
            canLoadNextPage = false;
            currentPage++;

            fetchCts = fetchCts.SafeRestartLinked(lifeCts!.Token);
            var ct = fetchCts.Token;

            ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Search] Requesting Page {currentPage} with search term: '{currentSearch}'");

            try
            {
                OnLoadingStateChanged?.Invoke(true);
                
                results.Clear();

                (var wearables, int total) = await wearablesProvider.GetAsync(
                    pageSize: CURRENT_PAGE_SIZE,
                    pageNumber: currentPage,
                    ct: ct,
                    sortingField: currentSort.OrderByOperation.ToSortingField(),
                    orderBy: currentSort.SortAscending ? IWearablesProvider.OrderBy.Ascending : IWearablesProvider.OrderBy.Descending,
                    category: string.Empty,
                    collectionType: IWearablesProvider.CollectionType.None,
                    name: currentSearch,
                    results: results,
                    network: "MATIC",
                    includeAmount: true
                );

                ct.ThrowIfCancellationRequested();

                ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Search] Response: wearables: {wearables.Count} result {results.Count} - {total}");
                
                totalCount = total;

                // Build the list of view models to add to the grid
                foreach (var wearable in wearables)
                {
                    var urn = wearable.GetUrn(); // This is the BASE URN
                    if (viewModelsByUrn.ContainsKey(urn)) continue;

                    // 1. Calculate the real number of items available to gift
                    int totalOwned = wearable.Amount;
                    int pendingCount = PendingGiftsCache.GetPendingCount(urn);
                    int displayAmount = totalOwned - pendingCount;

                    // 2. If all copies are pending transfer, don't show this item at all
                    if (displayAmount <= 0) continue;

                    var giftable = new WearableGiftable(wearable);
                    bool isEquipped = equippedContext.IsItemTypeEquipped(urn);

                    // 3. An item is giftable if it's not equipped, OR if the number of
                    //    available (non-pending) copies is greater than 1.
                    bool isGiftable = !isEquipped || displayAmount > 1;

                    viewModelUrnOrder.Add(urn);
                    viewModelsByUrn[urn] = new WearableViewModel(giftable, displayAmount, isEquipped, isGiftable);
                }

                if (currentPage == 1)
                {
                    bool hasResults = viewModelUrnOrder.Count > 0;
                    view.RegularResultsContainer.SetActive(hasResults);
                    view.NoResultsContainer.SetActive(!hasResults);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                adapter.RefreshData();
            }
            catch (OperationCanceledException)
            {
                /* swallow */
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
            }
            finally
            {
                isLoading = false;
                OnLoadingStateChanged?.Invoke(false);
                if (!ct.IsCancellationRequested && results.Count == 0 && pendingThumbnailLoads == 0)
                    OnAllLoadsFinished();
            }
        }

        public void RequestThumbnailLoad(int itemIndex)
        {
            string? urn = viewModelUrnOrder[itemIndex];
            var viewModel = viewModelsByUrn[urn];

            if (viewModel.ThumbnailState != ThumbnailState.NotLoaded) return;

            ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Thumbs] Requesting thumbnail for index {itemIndex}, URN: {urn}");
            
            // 1. Increment the counter as we are about to request a load
            pendingThumbnailLoads++;

            // 2. Update the view model state
            viewModelsByUrn[urn] = viewModel.WithState(ThumbnailState.Loading);

            // 3. Fire and forget the command
            loadThumbnailCommand.ExecuteAsync(viewModel.Giftable, urn, lifeCts.Token).Forget();
        }

        private void OnThumbnailLoaded(GiftingEvents.ThumbnailLoadedEvent evt)
        {
            ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Thumbs] Thumbnail loaded for URN: {evt.Urn}, Success: {evt.Success}. Pending loads: {pendingThumbnailLoads - 1}");
            
            if (viewModelsByUrn.TryGetValue(evt.Urn, out var vm))
            {
                var finalState = evt.Success ? ThumbnailState.Loaded : ThumbnailState.Error;
                viewModelsByUrn[evt.Urn] = vm.WithState(finalState, evt.Sprite);

                int index = viewModelUrnOrder.IndexOf(evt.Urn);
                if (index != -1)
                    adapter.RefreshItem(index);

                if (pendingThumbnailLoads > 0)
                    pendingThumbnailLoads--;

                if (pendingThumbnailLoads == 0)
                    OnAllLoadsFinished();
            }
        }

        private void ClearData()
        {
            view.NoResultsContainer.SetActive(false);
            view.RegularResultsContainer.SetActive(true);
            
            currentPage = 0;
            totalCount = int.MaxValue;
            SelectedUrn = null;
            isLoading = false;
            viewModelsByUrn.Clear();
            viewModelUrnOrder.Clear();
            OnSelectionChanged?.Invoke(null);

            pendingThumbnailLoads = 0;
            canLoadNextPage = true;
        }

        public string? GetItemNameByUrn(string urn)
        {
            if (viewModelsByUrn.TryGetValue(urn, out var vm))
                return vm.DisplayName;

            return null;
        }

        public void ForceSearch(string? searchText)
        {
            currentSearch = searchText ?? string.Empty;
            searchCts.SafeCancelAndDispose();
            fetchCts.SafeCancelAndDispose();

            ClearData();
            adapter.RefreshData();
            RequestNextPage().Forget();
        }

        public WearableViewModel GetViewModel(int itemIndex)
        {
            return viewModelsByUrn[viewModelUrnOrder[itemIndex]];
        }

        public Sprite? GetThumbnailByUrn(string urn)
        {
            if (viewModelsByUrn.TryGetValue(urn, out var vm))
                return vm.Thumbnail;
            return null;
        }

        public bool TryBuildStyleSnapshot(string urn, out GiftItemStyleSnapshot style)
        {
            style = default;

            if (!viewModelsByUrn.TryGetValue(urn, out var vm))
                return false;

            string? rarityId = string.IsNullOrEmpty(vm.RarityId) ? "base" : vm.RarityId;
            string? categoryId = string.IsNullOrEmpty(vm.CategoryId) ? null : vm.CategoryId;

            var rarityBg = wearableStylingCatalog.GetRarityBackground(rarityId);
            var flapColor = wearableStylingCatalog.GetRarityFlapColor(rarityId);
            var categoryIc = categoryId != null ? wearableStylingCatalog.GetCategoryIcon(categoryId) : null;

            style = new GiftItemStyleSnapshot(categoryIc, rarityBg, flapColor);
            return true;
        }

        public RectTransform GetRectTransform()
        {
            return rectTransform;
        }

        public CanvasGroup GetCanvasGroup()
        {
            return canvasGroup;
        }
    }
}