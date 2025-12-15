using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading;
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
    public class WearableGridPresenter : GiftingGridPresenterBase<GiftItemViewModel>
    {
        private readonly IWearablesProvider wearablesProvider;
        private readonly IWearableStylingCatalog stylingCatalog;
        private readonly List<ITrimmedWearable> resultsBuffer = new();
        private readonly BackpackGridSort currentSort = new(NftOrderByOperation.Date, false);

        public WearableGridPresenter(
            GiftingGridView view,
            SuperScrollGridAdapter<GiftItemViewModel> adapter,
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

        protected override async UniTask<(IEnumerable<GiftableAvatarAttachment> items, int total)> FetchDataAsync(int itemPageCount, int page, string search, CancellationToken ct)
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
                collectionType: IWearablesProvider.CollectionType.OnChain,
                name: search,
                network: "MATIC"
            );

            
            var giftables = new List<GiftableAvatarAttachment>(wearables.Count);
            foreach (var w in wearables)
            {
                giftables.Add(new GiftableAvatarAttachment(w, w.Amount));
            }

            return (giftables, total);
        }

        protected override GiftItemViewModel CreateViewModel(GiftableAvatarAttachment item, int amount, bool isEquipped, bool isGiftable)
        {
            return new GiftItemViewModel(item, amount, isEquipped, isGiftable, GiftableType.Wearable);
        }

        protected override int GetItemAmount(GiftableAvatarAttachment item)
        {
            return item.Amount;
        }

        protected override void UpdateEmptyState(bool isEmpty)
        {
            view.RegularResultsContainer.SetActive(!isEmpty);
            view.NoResultsContainer.SetActive(isEmpty);
        }

        protected override GiftItemViewModel  UpdateViewModelState(GiftItemViewModel vm, ThumbnailState state, Sprite? sprite)
        {
            return vm.WithState(state, sprite);
        }

        public override bool TryBuildStyleSnapshot(string urn, out GiftItemStyleSnapshot style)
        {
            style = default;

            if (!viewModelsByUrn.TryGetValue(urn, out var vm)) 
                return false;

            style = stylingCatalog.GetStyleSnapshot(vm.RarityId, vm.CategoryId);
            return true;
        }
    }
}