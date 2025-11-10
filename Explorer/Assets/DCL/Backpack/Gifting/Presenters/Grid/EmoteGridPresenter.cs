using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.Grid
{
    public class EmoteGridPresenter : IGiftingGridPresenter<EmoteViewModel>
    {
        private const int PAGE_SIZE = 16;
        private const int SEARCH_DEBOUNCE_MS = 500;

        public event Action<string?>? OnSelectionChanged;
        public event Action<bool>? OnLoadingStateChanged;
        public int CurrentItemCount => vmByUrn.Count;
        public string? SelectedUrn { get; private set; }
        public int ItemCount => urnOrder.Count;

        private readonly GiftingGridView view;
        private readonly SuperScrollGridAdapter<EmoteViewModel> adapter;
        private readonly IEmoteProvider emoteProvider;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IReadOnlyCollection<URN> embeddedEmoteIds;
        private readonly IEventBus eventBus;
        private readonly LoadGiftableItemThumbnailCommand loadThumb;
        private readonly IWearableStylingCatalog wearablesStylingCatalog;

        private readonly Dictionary<string, EmoteViewModel> vmByUrn = new();
        private readonly List<string> urnOrder = new();
        private readonly RectTransform rectTransform;
        private readonly CanvasGroup canvasGroup;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? fetchCts;
        private CancellationTokenSource? searchCts;
        private IDisposable? thumbSub;

        private string currentSearch = string.Empty;
        private readonly IEmoteProvider.OrderOperation currentOrder = new("date", isAscendent: false);
        private readonly bool onlyOnChain = false;

        private int currentPage  ;
        private int totalCount  = int.MaxValue;
        private int pendingThumbs  ;
        private bool isLoading  ;
        private bool canLoadNext = true;

        public EmoteGridPresenter(
            GiftingGridView view,
            SuperScrollGridAdapter<EmoteViewModel> adapter,
            IEmoteProvider emoteProvider,
            IWeb3IdentityCache identityCache,
            IReadOnlyCollection<URN> embeddedEmoteIds,
            IEventBus eventBus,
            LoadGiftableItemThumbnailCommand loadThumb,
            IWearableStylingCatalog wearableStylingCatalog)
        {
            this.view = view;
            this.adapter = adapter;
            this.emoteProvider = emoteProvider;
            this.identityCache = identityCache;
            this.embeddedEmoteIds = embeddedEmoteIds;
            this.eventBus = eventBus;
            this.loadThumb = loadThumb;
            wearablesStylingCatalog = wearablesStylingCatalog;

            rectTransform = view.GetComponent<RectTransform>();
            canvasGroup = view.GetComponent<CanvasGroup>();

            adapter.SetDataProvider(this);
        }

        public void Activate()
        {
            lifeCts = new CancellationTokenSource();

            thumbSub = eventBus.Subscribe<GiftingEvents.ThumbnailLoadedEvent>(OnThumbnailLoaded);
            adapter.OnNearEndOfScroll += OnNearEnd;
            adapter.OnItemSelected += OnItemSelected;

            ClearData();
            RequestNextPage().Forget();
        }

        public void Deactivate()
        {
            thumbSub?.Dispose();
            adapter.OnNearEndOfScroll -= OnNearEnd;
            adapter.OnItemSelected -= OnItemSelected;

            searchCts.SafeCancelAndDispose();
            fetchCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();

            HardClear();
        }

        public void SetSearchText(string search)
        {
            search ??= string.Empty;
            if (currentSearch == search) return;

            currentSearch = search;
            searchCts = searchCts.SafeRestartLinked(lifeCts!.Token);
            DebouncedSearchAsync(searchCts.Token).Forget();
        }

        private async UniTaskVoid DebouncedSearchAsync(CancellationToken ct)
        {
            await UniTask.Delay(SEARCH_DEBOUNCE_MS, cancellationToken: ct);
            if (ct.IsCancellationRequested) return;

            fetchCts.SafeCancelAndDispose();
            ClearData();
            adapter.RefreshData();
            RequestNextPage().Forget();
        }

        private void OnNearEnd()
        {
            if (canLoadNext && !isLoading && ItemCount < totalCount)
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
            canLoadNext = false;
            currentPage++;

            fetchCts = fetchCts.SafeRestartLinked(lifeCts!.Token);
            var ct = fetchCts.Token;

            try
            {
                OnLoadingStateChanged?.Invoke(true);
                ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Emotes] Request Page {currentPage} search='{currentSearch}'");

                // 1) Owned (on-chain/third-party) via provider
                using var ownedScope = ListPool<IEmote>.Get(out var owned);
                int ownedTotal = await emoteProvider.GetOwnedEmotesAsync(
                    identityCache.Identity!.Address,
                    ct,
                    new IEmoteProvider.OwnedEmotesRequestOptions(
                        pageNum: currentPage,
                        pageSize: PAGE_SIZE,
                        collectionId: null,
                        orderOperation: currentOrder,
                        name: currentSearch),
                    owned);

                // 2) Off-chain embedded emotes (optional union like Backpack)
                List<IEmote> pageEmotes;
                int grandTotal = ownedTotal;

                if (!onlyOnChain)
                {
                    using var embeddedScope = ListPool<IEmote>.Get(out var embedded);
                    await emoteProvider.GetEmotesAsync(embeddedEmoteIds, BodyShape.MALE, ct, embedded);

                    IEnumerable<IEmote> filtered = embedded;
                    if (!string.IsNullOrEmpty(currentSearch))
                        filtered = filtered.Where(e => e.GetName().IndexOf(currentSearch, StringComparison.OrdinalIgnoreCase) >= 0);

                    // order by name if needed (align with Backpack)
                    if (currentOrder.By == "name")
                        filtered = currentOrder.IsAscendent ? filtered.OrderBy(e => e.GetName()) : filtered.OrderByDescending(e => e.GetName());

                    var embeddedList = filtered.ToList();
                    grandTotal += embeddedList.Count;

                    // follow Backpack’s concat-at-end rule for pagination
                    int pageIndex = (currentPage - 1) * PAGE_SIZE;
                    int skipFromEmbedded = Mathf.Max(0, pageIndex - ownedTotal);

                    pageEmotes = owned.Concat(embeddedList.Skip(skipFromEmbedded))
                        .Take(PAGE_SIZE)
                        .ToList();
                }
                else
                {
                    pageEmotes = owned;
                }

                // First page -> show/hide containers
                if (currentPage == 1)
                {
                    bool hasResults = grandTotal > 0;
                    view.RegularResultsContainer.SetActive(hasResults);
                    view.NoResultsContainer.SetActive(!hasResults);
                }

                foreach (var emote in pageEmotes)
                {
                    var urn = emote.GetUrn();
                    if (vmByUrn.ContainsKey(urn)) continue;

                    var giftable = new EmoteGiftable(emote);
                    vmByUrn[urn] = new EmoteViewModel(giftable);
                    urnOrder.Add(urn);
                }

                totalCount = grandTotal;

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
                
                if (!ct.IsCancellationRequested && pendingThumbs == 0)
                    OnAllLoadsFinished();
            }
        }

        public void RequestThumbnailLoad(int itemIndex)
        {
            string urn = urnOrder[itemIndex];
            var vm = vmByUrn[urn];
            if (vm.ThumbnailState != ThumbnailState.NotLoaded) return;

            pendingThumbs++;
            vmByUrn[urn] = vm.WithState(ThumbnailState.Loading);

            loadThumb.ExecuteAsync(vm.Giftable, urn, lifeCts!.Token).Forget();
        }

        private void OnThumbnailLoaded(GiftingEvents.ThumbnailLoadedEvent evt)
        {
            if (vmByUrn.TryGetValue(evt.Urn, out var vm))
            {
                var final = evt.Success ? ThumbnailState.Loaded : ThumbnailState.Error;
                vmByUrn[evt.Urn] = vm.WithState(final, evt.Sprite);

                int index = urnOrder.IndexOf(evt.Urn);
                if (index >= 0) adapter.RefreshItem(index);

                if (pendingThumbs > 0) pendingThumbs--;
                if (pendingThumbs == 0) OnAllLoadsFinished();
            }
        }

        private void OnAllLoadsFinished()
        {
            if (lifeCts?.IsCancellationRequested == true) return;
            canLoadNext = true;
            if (adapter.IsNearEnd && !isLoading && ItemCount < totalCount)
                RequestNextPage().Forget();
        }

        private void ClearData()
        {
            view.NoResultsContainer.SetActive(false);
            view.RegularResultsContainer.SetActive(true);

            SelectedUrn = null;
            currentPage = 0;
            totalCount = int.MaxValue;
            isLoading = false;
            pendingThumbs = 0;
            canLoadNext = true;

            vmByUrn.Clear();
            urnOrder.Clear();
            OnSelectionChanged?.Invoke(null);
        }

        private void HardClear()
        {
            ClearData();
            adapter.RefreshData();
            adapter.RefreshAllShownItem();
        }

        public EmoteViewModel GetViewModel(int itemIndex)
        {
            return vmByUrn[urnOrder[itemIndex]];
        }

        public string? GetItemNameByUrn(string urn)
        {
            return vmByUrn.TryGetValue(urn, out var vm) ? vm.DisplayName : null;
        }

        public Sprite? GetThumbnailByUrn(string urn)
        {
            if (vmByUrn.TryGetValue(urn, out var vm))
                return vm.Thumbnail;
            return null;
        }

        public bool TryBuildStyleSnapshot(string urn, out GiftItemStyleSnapshot style)
        {
            style = default;

            if (!vmByUrn.TryGetValue(urn, out var vm))
                return false;

            string? rarityId = string.IsNullOrEmpty(vm.RarityId) ? "base" : vm.RarityId;
            string? categoryId = string.IsNullOrEmpty(vm.CategoryId) ? null : vm.CategoryId;

            var rarityBg = wearablesStylingCatalog.GetRarityBackground(rarityId);
            var flapColor = wearablesStylingCatalog.GetRarityFlapColor(rarityId);
            var categoryIc = categoryId != null ? wearablesStylingCatalog.GetCategoryIcon(categoryId) : null;

            style = new GiftItemStyleSnapshot(categoryIc, rarityBg, flapColor);
            return true;
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