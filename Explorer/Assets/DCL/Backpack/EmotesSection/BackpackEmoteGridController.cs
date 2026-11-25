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
using System.Diagnostics;
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

        private static readonly bool TRACE_TIMINGS = Environment.GetEnvironmentVariable("DCL_BACKPACK_EMOTE_TIMINGS") == "1";

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
        private CancellationTokenSource? cardBindingsCancellationToken;
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
            cardBindingsCancellationToken.SafeCancelAndDispose();
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

            var timing = new CatalogTiming(pageNumber);
            if (TRACE_TIMINGS)
                timing.Log("page-start", timing.Start, string.Empty);

            currentPage = pageNumber;
            if (reconfigurePageSelector)
                pageSelectorController.SetActive(false);

            catalogRetry.gameObject.SetActive(false);
            view.NoSearchResults.SetActive(false);
            view.NoCategoryResults.SetActive(false);
            view.RegularResults.SetActive(true);
            SetGridAsLoading();
            RequestPageAsync(loadElementsCancellationToken.Token).Forget();
            return;

            async UniTaskVoid RequestPageAsync(CancellationToken ct)
            {
                using var ownedScope = ListPool<ITrimmedEmote>.Get(out var owned);
                using var baseScope = ListPool<IEmote>.Get(out var baseEmotes);
                bool ownedReady = false;
                bool baseReady = onChainEmotesOnly;
                bool catalogFailed = false;
                bool bound = false;
                int ownedTotal = 0;
                int configuredTotal = -1;

                try
                {
                    if (!baseReady && pageNumber == 1)
                    {
                        long start = CatalogTiming.Timestamp();
                        foreach (URN pointer in emoteStorage.BaseEmotesUrns)
                        {
                            if (!emoteStorage.TryGetElement(pointer.Shorten(), out var cached) || !cached.Model.Succeeded || cached.DTO?.Metadata == null)
                                continue;

                            if (!cached.TrimmedModel.Succeeded)
                                cached.PopulateTrimmedModel();

                            baseEmotes.Add(cached);
                        }

                        FilterAndSortBaseEmotes(baseEmotes);
                        if (TRACE_TIMINGS)
                            timing.Log("cached-base", start, $"count={baseEmotes.Count}");

                        if (baseEmotes.Count > 0)
                            Publish("cached-base");
                    }

                    var ownedRequest = LoadOwnedAsync();
                    try
                    {
                        if (!baseReady)
                            await LoadBaseAsync();
                    }
                    finally { await ownedRequest; }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    ReportHub.LogException(e, ReportCategory.BACKPACK);
                    if (!ct.IsCancellationRequested && !bound)
                        ShowCatalogError();
                }

                async UniTask LoadOwnedAsync()
                {
                    long start = CatalogTiming.Timestamp();
                    var result = await emoteProvider.GetTrimmedByParamsAsync(
                        new IEmoteProvider.OwnedEmotesRequestOptions(pageNum: pageNumber, pageSize: CURRENT_PAGE_SIZE,
                            collectionId: null, orderOperation: currentOrder, name: currentSearch, propagateErrors: true),
                        ct, owned).SuppressToResultAsync(ReportCategory.BACKPACK);

                    if (TRACE_TIMINGS)
                        timing.Log("owned", start, $"count={owned.Count} success={result.Success} canceled={ct.IsCancellationRequested}");

                    if (ct.IsCancellationRequested) return;
                    ownedReady = true;
                    catalogFailed |= !result.Success;
                    ownedTotal = result.Success ? result.Value.totalAmount : owned.Count;
                    if (result.Success)
                        ownedNftFilter.Prune(GiftableType.Emote);

                    Publish("owned");
                }

                async UniTask LoadBaseAsync()
                {
                    IReadOnlyList<URN> pointers = emoteStorage.BaseEmotesUrns;
                    long start = CatalogTiming.Timestamp();
                    using var fetchedScope = ListPool<IEmote>.Get(out var fetched);
                    var result = await emoteProvider.GetMetadataByPointersAsync(pointers, ct, fetched)
                                                    .SuppressToResultAsync(ReportCategory.BACKPACK);

                    if (TRACE_TIMINGS)
                        timing.Log("base-metadata", start, $"count={fetched.Count} requested={pointers.Count} success={result.Success} canceled={ct.IsCancellationRequested}");

                    if (ct.IsCancellationRequested) return;
                    baseReady = true;
                    // A timeout may return only some pointers. Keep any usable cached entries it omitted.
                    using var knownScope = DictionaryPool<URN, IEmote>.Get(out var known);
                    foreach (var cached in baseEmotes)
                        known[cached.GetUrn()] = cached;
                    foreach (var emote in fetched)
                        known[emote.GetUrn()] = emote;

                    baseEmotes.Clear();
                    foreach (URN pointer in pointers)
                        if (known.TryGetValue(EmoteComponentsUtils.ConvertLegacyEmoteUrnToOnChain(pointer).Shorten(), out var emote))
                            baseEmotes.Add(emote);

                    catalogFailed |= !result.Success || baseEmotes.Count < pointers.Count;
                    FilterAndSortBaseEmotes(baseEmotes);
                    Publish("base-metadata");
                }

                void Publish(string phase)
                {
                    bool complete = ownedReady && baseReady;
                    using var pageScope = ListPool<ITrimmedEmote>.Get(out var page);
                    if (ownedReady)
                        for (int i = 0; i < owned.Count && page.Count < CURRENT_PAGE_SIZE; i++)
                            page.Add(owned[i]);

                    int total = ownedTotal + baseEmotes.Count;
                    int firstBase = Math.Max(0, ((pageNumber - 1) * CURRENT_PAGE_SIZE) - ownedTotal);
                    for (int i = firstBase; i < baseEmotes.Count && page.Count < CURRENT_PAGE_SIZE; i++)
                        page.Add(baseEmotes[i]);

                    if (page.Count == 0)
                    {
                        if (!complete) return;
                        if (catalogFailed)
                        {
                            if (!bound)
                                ShowCatalogError();

                            if (TRACE_TIMINGS)
                                timing.Log("catalog-complete", timing.Start, $"count={usedPoolItems.Count} total={total} partial=True");

                            return;
                        }

                        view.NoSearchResults.SetActive(!string.IsNullOrEmpty(currentSearch));
                        view.NoCategoryResults.SetActive(!string.IsNullOrEmpty(currentCategory));
                        view.RegularResults.SetActive(string.IsNullOrEmpty(currentSearch) && string.IsNullOrEmpty(currentCategory));
                    }

                    long start = CatalogTiming.Timestamp();
                    bool rowsChanged = !bound || !HasSameRows(page);
                    if (rowsChanged)
                    {
                        cardBindingsCancellationToken = cardBindingsCancellationToken.SafeRestartLinked(ct);
                        if (bound)
                            SetGridAsLoading();

                        SetGridElements(page, cardBindingsCancellationToken.Token, timing);
                        bound = true;
                    }

                    if ((complete || reconfigurePageSelector) && configuredTotal != total)
                    {
                        pageSelectorController.Configure(total, CURRENT_PAGE_SIZE, pageNumber);
                        configuredTotal = total;
                    }

                    if (TRACE_TIMINGS && rowsChanged)
                        timing.Log("cards-bound", start, $"count={page.Count} total={total} provisional={!complete} phase={phase}");

                    if (TRACE_TIMINGS && complete)
                        timing.Log("catalog-complete", start, $"count={page.Count} total={total} partial={catalogFailed}");
                }
            }
        }

        private bool HasSameRows(IReadOnlyList<ITrimmedEmote> page)
        {
            if (usedPoolItems.Count != page.Count) return false;

            for (int i = 0; i < page.Count; i++)
                if (!usedPoolItems.TryGetValue(page[i].GetUrn(), out var viewForUrn) || viewForUrn != loadingResults[i])
                    return false;

            return true;
        }

        private void FilterAndSortBaseEmotes(List<IEmote> emotes)
        {
            if (!string.IsNullOrEmpty(currentSearch))
                emotes.RemoveAll(emote => !emote.GetName().Contains(currentSearch, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(currentCategory))
                emotes.RemoveAll(emote => emote.GetCategory() != currentCategory);

            if (currentOrder.By == "name")
                emotes.Sort((left, right) => currentOrder.IsAscending
                    ? string.Compare(left.GetName(), right.GetName(), StringComparison.CurrentCulture)
                    : string.Compare(right.GetName(), left.GetName(), StringComparison.CurrentCulture));
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

        private void SetGridElements(IReadOnlyList<ITrimmedEmote> emotes, CancellationToken ct, CatalogTiming timing)
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
                WaitForThumbnailAsync(emotes[i], backpackItemView, ct, timing).Forget();
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
            CancellationToken ct = cardBindingsCancellationToken!.Token;
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

        private async UniTaskVoid WaitForThumbnailAsync(ITrimmedEmote emote, BackpackEmoteGridItemView itemView, CancellationToken ct, CatalogTiming timing)
        {
            const int MAX_ATTEMPTS = 2;
            for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
            {
                long stageStart = CatalogTiming.Timestamp();
                // The ECS provider combines scheduling, cache access, transport and texture extraction in one wait.
                string source = string.Empty;
                if (TRACE_TIMINGS)
                    source = emote.ThumbnailAssetResult switch
                    {
                        { IsInitialized: true, Succeeded: true } => "memory",
                        { IsInitialized: false } => "in-flight",
                        _ => "load",
                    };

                var result = await thumbnailProvider.GetAsync(emote, ct).SuppressToResultAsync(ReportCategory.THUMBNAILS);

                if (result.Success && !ct.IsCancellationRequested)
                    itemView.SetThumbnail(result.Value);

                if (TRACE_TIMINGS)
                {
                    string outcome;
                    if (ct.IsCancellationRequested)
                        outcome = "canceled";
                    else if (!result.Success)
                        outcome = "error";
                    else
                        outcome = itemView.WearableThumbnail.enabled ? "artwork" : "fallback";

                    timing.Log("thumbnail", stageStart, $"urn={emote.GetUrn()} source={source} attempt={attempt} outcome={outcome}");
                }

                if (attempt == MAX_ATTEMPTS || ct.IsCancellationRequested ||
                    emote.ThumbnailAssetResult is not { IsInitialized: true, Succeeded: false, Cancelled: false })
                    return;

                // The ECS resolver retires the timed-out promise before a new attempt uses its slot.
                if (await UniTask.DelayFrame(1, PlayerLoopTiming.LastPostLateUpdate, ct).SuppressCancellationThrow())
                    return;
            }
        }

        private readonly struct CatalogTiming
        {
            public readonly long Start;
            private readonly int page;

            public CatalogTiming(int page)
            {
                this.page = page;
                Start = Timestamp();
            }

            public static long Timestamp() => TRACE_TIMINGS ? Stopwatch.GetTimestamp() : 0;

            public void Log(string stage, long stageStart, string details)
            {
                long now = Stopwatch.GetTimestamp();
                double elapsedMs = (now - Start) * 1000d / Stopwatch.Frequency;
                double stageMs = (now - stageStart) * 1000d / Stopwatch.Frequency;
                ReportHub.LogProductionInfo(FormattableString.Invariant($"[BackpackEmoteTiming] request={Start} page={page} stage={stage} elapsedMs={elapsedMs:F3} stageMs={stageMs:F3} {details}"), ReportHandler.DebugLog);
            }
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
