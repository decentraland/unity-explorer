using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
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
        public string? SelectedUrn { get; set; }
        public int ItemCount => viewModelUrnOrder.Count;

        private readonly GiftingGridView view;
        private readonly SuperScrollGridAdapter<WearableViewModel> adapter;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEventBus eventBus;
        private readonly LoadGiftableItemThumbnailCommand loadThumbnailCommand;
        private readonly NFTColorsSO rarityColorMappings;
        private readonly NftTypeIconSO categoryIconsMapping;
        private readonly NftTypeIconSO rarityBackgroundsMapping;

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
            NFTColorsSO rarityColorMappings,
            NftTypeIconSO categoryIconsMapping,
            NftTypeIconSO rarityBackgroundsMapping)
        {
            this.view = view;
            this.adapter = adapter;
            this.wearablesProvider = wearablesProvider;
            this.eventBus = eventBus;
            this.loadThumbnailCommand = loadThumbnailCommand;
            this.rarityColorMappings = rarityColorMappings;
            this.categoryIconsMapping = categoryIconsMapping;
            this.rarityBackgroundsMapping = rarityBackgroundsMapping;

            rectTransform = view.GetComponent<RectTransform>();
            canvasGroup = view.GetComponent<CanvasGroup>();


            adapter.SetDataProvider(this);
        }

        public void Activate()
        {
            lifeCts = new CancellationTokenSource();

            thumbnailLoadedSubscription = eventBus
                .Subscribe<GiftingEvents.ThumbnailLoadedEvent>(OnThumbnailLoaded);

            adapter.OnNearEndOfScroll += OnNearEndOfScroll;
            adapter.OnItemSelected += OnItemSelected;

            ClearData();
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
            ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Search] SetSearchText received: '{searchText}'");
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
                results.Clear();
                (var wearables, int total) = await wearablesProvider.GetAsync(
                    pageSize: CURRENT_PAGE_SIZE,
                    pageNumber: currentPage,
                    ct: ct,
                    sortingField: currentSort.OrderByOperation.ToSortingField(),
                    orderBy: currentSort.SortAscending ? IWearablesProvider.OrderBy.Ascending : IWearablesProvider.OrderBy.Descending,
                    category: string.Empty,
                    collectionType: IWearablesProvider.CollectionType.OnChain | IWearablesProvider.CollectionType.ThirdParty,
                    name: currentSearch,
                    results: results
                );

                ct.ThrowIfCancellationRequested();

                ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Search] Response: wearables: {wearables.Count} result {results.Count} - {total}");

                totalCount = total;

                if (currentPage == 1)
                {
                    bool hasResults = total > 0;
                    view.RegularResultsContainer.SetActive(hasResults);
                    view.NoResultsContainer.SetActive(!hasResults);
                }

                foreach (var wearable in wearables)
                {
                    var urn = wearable.GetUrn();
                    if (viewModelsByUrn.ContainsKey(urn)) continue;

                    viewModelUrnOrder.Add(urn);
                    viewModelsByUrn[urn] = new WearableViewModel(wearable);
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

                if (!ct.IsCancellationRequested && results.Count == 0 && pendingThumbnailLoads == 0)
                    OnAllLoadsFinished();
            }
        }

        public void RequestThumbnailLoad(int itemIndex)
        {
            string? urn = viewModelUrnOrder[itemIndex];
            var vm = viewModelsByUrn[urn];

            if (vm.ThumbnailState != ThumbnailState.NotLoaded) return;

            ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Thumbs] Requesting thumbnail for index {itemIndex}, URN: {urn}");
            
            // 1. Increment the counter as we are about to request a load
            pendingThumbnailLoads++;

            // 2. Update the view model state
            viewModelsByUrn[urn] = vm.WithState(ThumbnailState.Loading);

            // 3. Fire and forget the command
            loadThumbnailCommand.ExecuteAsync(vm.Source, lifeCts.Token).Forget();
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
                return vm.Source.GetName();

            return null;
        }

        public WearableViewModel GetViewModel(int itemIndex)
        {
            return viewModelsByUrn[viewModelUrnOrder[itemIndex]];
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