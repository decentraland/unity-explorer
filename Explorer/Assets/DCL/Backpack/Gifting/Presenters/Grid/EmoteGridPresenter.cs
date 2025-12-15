using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.Grid
{
    public class EmoteGridPresenter : GiftingGridPresenterBase<GiftItemViewModel>
    {
        private readonly IEmoteProvider emoteProvider;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWearableStylingCatalog stylingCatalog;
        private readonly IEmoteProvider.OrderOperation currentOrder = new("date", isAscendent: false);

        public EmoteGridPresenter(
            GiftingGridView view,
            SuperScrollGridAdapter<GiftItemViewModel> adapter,
            IEventBus eventBus,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand,
            IAvatarEquippedStatusProvider equippedStatusProvider,
            IPendingTransferService pendingTransferService,
            IEmoteProvider emoteProvider,
            IWeb3IdentityCache identityCache,
            IWearableStylingCatalog stylingCatalog,
            IWearableStorage wearableStorage,
            IEmoteStorage  emoteStorage)
            : base(view,
                adapter,
                eventBus,
                loadThumbnailCommand,
                equippedStatusProvider,
                pendingTransferService,
                wearableStorage,
                emoteStorage)
        {
            this.emoteProvider = emoteProvider;
            this.identityCache = identityCache;
            this.stylingCatalog = stylingCatalog;

            adapter.UseWearableStyling(stylingCatalog);
        }

        protected override async UniTask<(IEnumerable<GiftableAvatarAttachment> items, int total)> FetchDataAsync(int pageItemCount, int page, string search, CancellationToken ct)
        {
            ReportHub.Log(ReportCategory.GIFTING, $"[Gifting-Emotes] Request Page {page} search='{search}'");

            using var pageEmotesScope = ListPool<IEmote>.Get(out var pageEmotes);

            var requestOptions = new IEmoteProvider.OwnedEmotesRequestOptions(
                pageNum: page,
                pageSize: pageItemCount,
                collectionId: null,
                orderOperation: currentOrder,
                name: search,
                includeAmount: true);

            int total = await emoteProvider.GetOwnedEmotesAsync(
                identityCache.Identity!.Address,
                ct,
                requestOptions,
                pageEmotes);

            var giftables = new List<GiftableAvatarAttachment>(pageEmotes.Count);
            foreach (var e in pageEmotes)
                giftables.Add(new GiftableAvatarAttachment(e, 1));

            return (giftables, total);
        }

        protected override GiftItemViewModel CreateViewModel(GiftableAvatarAttachment item, int amount, bool isEquipped, bool isGiftable)
        {
            // Safe cast because FetchDataAsync only produces EmoteGiftable
            return new GiftItemViewModel (item, amount, isEquipped, isGiftable, GiftableType.Emote);
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

        protected override GiftItemViewModel UpdateViewModelState(GiftItemViewModel vm, ThumbnailState state, Sprite? sprite)
        {
            return vm.WithState(state, sprite);
        }

        public override bool TryBuildStyleSnapshot(string urn, out GiftItemStyleSnapshot style)
        {
            style = default;

            if (!viewModelsByUrn.TryGetValue(urn, out var vm)) 
                return false;

            style = stylingCatalog.GetStyleSnapshot(vm.RarityId, "emote");
            return true;
        }
    }
}