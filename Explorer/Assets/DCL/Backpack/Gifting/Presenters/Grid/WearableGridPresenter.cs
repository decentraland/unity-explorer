using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class WearableGridPresenter : GiftingGridPresenterBase<WearableViewModel>
    {
        private readonly IWearablesProvider wearablesProvider;
        private readonly IWearableStylingCatalog stylingCatalog;

        // Reusable buffer for the provider to avoid allocations
        private readonly List<IWearable> resultsBuffer = new();
        private readonly BackpackGridSort currentSort = new(NftOrderByOperation.Date, false);

        public WearableGridPresenter(
            GiftingGridView view,
            SuperScrollGridAdapter<WearableViewModel> adapter,
            IEventBus eventBus,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand,
            IAvatarEquippedStatusProvider equippedStatusProvider,
            IPendingTransferService pendingTransferService,
            IWearablesProvider wearablesProvider,
            IWearableStylingCatalog stylingCatalog,
            IWearableStorage wearableStorage,
            IEmoteStorage  emoteStorage)
            : base(view,
                adapter,
                eventBus,
                loadThumbnailCommand,
                equippedStatusProvider,
                pendingTransferService,
                wearableStorage, emoteStorage)
        {
            this.wearablesProvider = wearablesProvider;
            this.stylingCatalog = stylingCatalog;

            adapter.UseWearableStyling(stylingCatalog);
        }

        protected override async UniTask<(IEnumerable<IGiftable> items, int total)> FetchDataAsync(int itemPageCount, int page, string search, CancellationToken ct)
        {
            resultsBuffer.Clear();

            ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Search] Requesting Page {page} search: '{search}'");

            (var wearables, int total) = await wearablesProvider.GetAsync(
                pageSize: itemPageCount,
                pageNumber: page,
                ct: ct,
                sortingField: currentSort.OrderByOperation.ToSortingField(),
                orderBy: currentSort.SortAscending ? IWearablesProvider.OrderBy.Ascending : IWearablesProvider.OrderBy.Descending,
                category: string.Empty,
                collectionType: IWearablesProvider.CollectionType.None,
                name: search,
                results: resultsBuffer,
                network: "MATIC",
                includeAmount: true
            );

            // Map to IGiftable. We create a new list because resultsBuffer is cleared every request.
            var giftables = new List<IGiftable>(wearables.Count);
            foreach (var w in wearables)
            {
                giftables.Add(new WearableGiftable(w));
            }

            return (giftables, total);
        }

        protected override WearableViewModel CreateViewModel(IGiftable item, int amount, bool isEquipped, bool isGiftable)
        {
            // Safe cast because FetchDataAsync only produces WearableGiftable
            return new WearableViewModel((WearableGiftable)item, amount, isEquipped, isGiftable);
        }

        protected override int GetItemAmount(IGiftable item)
        {
            return ((WearableGiftable)item).Wearable.Amount;
        }

        protected override void UpdateEmptyState(bool isEmpty)
        {
            view.RegularResultsContainer.SetActive(!isEmpty);
            view.NoResultsContainer.SetActive(isEmpty);
        }

        protected override WearableViewModel UpdateViewModelState(WearableViewModel vm, ThumbnailState state, Sprite? sprite)
        {
            return vm.WithState(state, sprite);
        }

        public override bool TryBuildStyleSnapshot(string urn, out GiftItemStyleSnapshot style)
        {
            style = default;

            if (!viewModelsByUrn.TryGetValue(urn, out var vm)) 
                return false;

            string rarityId = vm.RarityId ?? "base";
            string? categoryId = vm.CategoryId;

            var rarityBg = stylingCatalog.GetRarityBackground(rarityId);
            var flapColor = stylingCatalog.GetRarityFlapColor(rarityId);
            var categoryIc = categoryId != null ? stylingCatalog.GetCategoryIcon(categoryId) : null;

            style = new GiftItemStyleSnapshot(categoryIc, rarityBg, flapColor);
            return true;
        }
    }
}