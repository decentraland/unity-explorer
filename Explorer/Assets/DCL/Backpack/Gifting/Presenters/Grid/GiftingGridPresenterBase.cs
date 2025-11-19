using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Cache;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.Grid
{
    public abstract class GiftingGridPresenterBase<TViewModel> : IGiftingGridPresenter<TViewModel>
        where TViewModel : IGiftableItemViewModel
    {
        private const int SEARCH_DEBOUNCE_MS = 500;

        protected readonly GiftingGridView view;
        protected readonly SuperScrollGridAdapter<TViewModel> adapter;
        protected readonly IEventBus eventBus;
        protected readonly LoadGiftableItemThumbnailCommand loadThumbnailCommand;
        protected readonly IAvatarEquippedStatusProvider equippedStatusProvider;
        protected readonly IPendingTransferService pendingTransferService;

        protected readonly Dictionary<string, TViewModel> viewModelsByUrn = new();
        protected readonly List<string> viewModelUrnOrder = new();
        protected int currentPage  ;
        protected int totalCount = int.MaxValue;
        protected string currentSearch = string.Empty;
        protected bool isLoading  ;
        protected bool canLoadNextPage = true;
        protected int pendingThumbnailLoads  ;

        protected CancellationTokenSource? lifeCts;
        protected CancellationTokenSource? fetchCts;
        protected CancellationTokenSource? searchCts;
        private IDisposable? thumbnailLoadedSubscription;

        public event Action<string?>? OnSelectionChanged;
        public event Action<bool>? OnLoadingStateChanged;

        public int CurrentItemCount => viewModelsByUrn.Count;
        public int ItemCount => viewModelUrnOrder.Count;
        public string? SelectedUrn { get; private set; }

        protected GiftingGridPresenterBase(
            GiftingGridView view,
            SuperScrollGridAdapter<TViewModel> adapter,
            IEventBus eventBus,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand,
            IAvatarEquippedStatusProvider equippedStatusProvider,
            IPendingTransferService pendingTransferService)
        {
            this.view = view;
            this.adapter = adapter;
            this.eventBus = eventBus;
            this.loadThumbnailCommand = loadThumbnailCommand;
            this.equippedStatusProvider = equippedStatusProvider;
            this.pendingTransferService = pendingTransferService;

            adapter.SetDataProvider(this);
        }

        public void Activate()
        {
            lifeCts = new CancellationTokenSource();
            thumbnailLoadedSubscription = eventBus.Subscribe<GiftingEvents.ThumbnailLoadedEvent>(OnThumbnailLoaded);
            adapter.OnNearEndOfScroll += OnNearEndOfScroll;
            adapter.OnItemSelected += OnItemSelected;

            ClearData();
            adapter.RefreshData();
            RequestNextPageAsync(lifeCts.Token).Forget();
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

        public void SetSearchText(string searchText)
        {
            searchText ??= string.Empty;
            if (currentSearch == searchText) return;
            currentSearch = searchText;

            searchCts = searchCts.SafeRestartLinked(lifeCts!.Token);
            DebouncedSearchAsync(searchCts.Token).Forget();
        }

        private async UniTaskVoid DebouncedSearchAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(SEARCH_DEBOUNCE_MS, cancellationToken: ct);
                if (ct.IsCancellationRequested) return;

                fetchCts.SafeCancelAndDispose();
                ClearData();
                adapter.RefreshData();

                // Await here to catch exceptions inside the try block
                await RequestNextPageAsync(ct);
            }
            catch (OperationCanceledException)
            {
                /* Expected */
            }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING)); }
        }

        protected async UniTask RequestNextPageAsync(CancellationToken ct)
        {
            if (isLoading) return;

            isLoading = true;
            canLoadNextPage = false;
            currentPage++;

            fetchCts = fetchCts.SafeRestartLinked(ct);
            var localCt = fetchCts.Token;

            try
            {
                OnLoadingStateChanged?.Invoke(true);

                // Abstract call to get data
                (var items, int total) = await FetchDataAsync(currentPage, currentSearch, localCt);

                totalCount = total;

                foreach (var item in items)
                {
                    string urn = item.Urn;
                    if (viewModelsByUrn.ContainsKey(urn)) continue;

                    // Common Logic
                    int totalOwned = GetItemAmount(item);
                    int pendingCount = pendingTransferService.GetPendingCount(urn);
                    int displayAmount = totalOwned - pendingCount;

                    if (displayAmount <= 0) continue;

                    bool isEquipped = equippedStatusProvider.IsEquipped(urn);
                    bool isGiftable = !isEquipped || displayAmount > 1;

                    viewModelUrnOrder.Add(urn);
                    viewModelsByUrn[urn] = CreateViewModel(item, displayAmount, isEquipped, isGiftable);
                }

                UpdateEmptyState(currentPage == 1 && viewModelUrnOrder.Count == 0);

                await UniTask.Yield(PlayerLoopTiming.Update, localCt);
                adapter.RefreshData();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING)); }
            finally
            {
                isLoading = false;
                OnLoadingStateChanged?.Invoke(false);
                if (!localCt.IsCancellationRequested && pendingThumbnailLoads == 0)
                    OnAllLoadsFinished();
            }
        }

        protected abstract UniTask<(IEnumerable<IGiftable> items, int total)> FetchDataAsync(int page, string search, CancellationToken ct);
        protected abstract TViewModel CreateViewModel(IGiftable item, int amount, bool isEquipped, bool isGiftable);
        protected abstract int GetItemAmount(IGiftable item);
        protected abstract void UpdateEmptyState(bool isEmpty);

        private void OnNearEndOfScroll()
        {
            if (canLoadNextPage && !isLoading && ItemCount < totalCount)
                RequestNextPageAsync(lifeCts!.Token).Forget();
        }

        private void OnItemSelected(string urn)
        {
            if (viewModelsByUrn.TryGetValue(urn, out var vm) && !vm.IsGiftable) return;
            SelectedUrn = SelectedUrn == urn ? null : urn;
            OnSelectionChanged?.Invoke(SelectedUrn);
            adapter.RefreshAllShownItem();
        }

        public void RequestThumbnailLoad(int itemIndex)
        {
            string urn = viewModelUrnOrder[itemIndex];
            var vm = viewModelsByUrn[urn];
            if (vm.ThumbnailState != ThumbnailState.NotLoaded) return;

            pendingThumbnailLoads++;
            //viewModelsByUrn[urn] = (TViewModel)vm.WithState(ThumbnailState.Loading); // Cast might need interface adjustment or abstract method
            loadThumbnailCommand.ExecuteAsync(vm.Giftable, urn, lifeCts!.Token).Forget();
        }

        private void OnThumbnailLoaded(GiftingEvents.ThumbnailLoadedEvent evt)
        {
            if (viewModelsByUrn.TryGetValue(evt.Urn, out var vm))
            {
                var final = evt.Success ? ThumbnailState.Loaded : ThumbnailState.Error;
                // Note: TViewModel needs a way to update state. 
                // Ideally IGiftableItemViewModel has a 'WithState' or we use the abstract CreateViewModel again.
                // For now assuming TViewModel is struct and we replace it.
                viewModelsByUrn[evt.Urn] = UpdateViewModelState(vm, final, evt.Sprite);

                int index = viewModelUrnOrder.IndexOf(evt.Urn);
                if (index >= 0) adapter.RefreshItem(index);

                if (pendingThumbnailLoads > 0) pendingThumbnailLoads--;
                if (pendingThumbnailLoads == 0) OnAllLoadsFinished();
            }
        }

        protected abstract TViewModel UpdateViewModelState(TViewModel vm, ThumbnailState state, Sprite? sprite);

        private void OnAllLoadsFinished()
        {
            if (lifeCts?.IsCancellationRequested == true) return;
            canLoadNextPage = true;
            if (adapter.IsNearEnd && !isLoading && ItemCount < totalCount)
                RequestNextPageAsync(lifeCts!.Token).Forget();
        }

        private void ClearData()
        {
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

        private void HardClear()
        {
            ClearData();
            adapter.RefreshData();
            adapter.RefreshAllShownItem();
        }

        // Interface impl
        public void PrepareForLoading(EquippedItemContext context)
        {
            /* No-op, we use injected service now */
        }

        public TViewModel GetViewModel(int itemIndex)
        {
            return viewModelsByUrn[viewModelUrnOrder[itemIndex]];
        }

        public string? GetItemNameByUrn(string urn)
        {
            return viewModelsByUrn.TryGetValue(urn, out var vm) ? vm.DisplayName : null;
        }

        public Sprite? GetThumbnailByUrn(string urn)
        {
            return viewModelsByUrn.TryGetValue(urn, out var vm) ? vm.Thumbnail : null;
        }

        public void ForceSearch(string? searchText)
        {
            SetSearchText(searchText ?? "");
        }

        public RectTransform GetRectTransform()
        {
            return view.GetComponent<RectTransform>();
        }

        public CanvasGroup GetCanvasGroup()
        {
            return view.GetComponent<CanvasGroup>();
        }

        public abstract bool TryBuildStyleSnapshot(string urn, out GiftItemStyleSnapshot style);
    }
}