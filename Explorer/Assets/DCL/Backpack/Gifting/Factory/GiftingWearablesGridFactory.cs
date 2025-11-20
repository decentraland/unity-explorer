using System.Collections.Generic;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Presenters.Grid;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;
using DCL.Web3.Identities;
using Utility;

namespace DCL.Backpack.Gifting.Factory
{
    public interface IGiftingGridPresenterFactory
    {
        IGiftingGridPresenter CreateWearablesPresenter(GiftingGridView view, SuperScrollGridAdapter<WearableViewModel> adapter);
        IGiftingGridPresenter CreateEmotesPresenter(GiftingGridView view, SuperScrollGridAdapter<EmoteViewModel> adapter);
    }

    public sealed class GiftingGridPresenterFactory : IGiftingGridPresenterFactory
    {
        private readonly IEventBus eventBus;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEmoteProvider emoteProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly LoadGiftableItemThumbnailCommand loadThumbnailCommand;
        private readonly IWearableStylingCatalog wearableStylingCatalog;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;

        // New Dependencies
        private readonly IPendingTransferService pendingTransferService;
        private readonly IAvatarEquippedStatusProvider equippedStatusProvider;

        public GiftingGridPresenterFactory(
            IEventBus eventBus,
            IWearablesProvider wearablesProvider,
            IEmoteProvider emoteProvider,
            IWeb3IdentityCache web3IdentityCache,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand,
            IWearableStylingCatalog wearableStylingCatalog,
            IPendingTransferService pendingTransferService,
            IAvatarEquippedStatusProvider equippedStatusProvider,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage)
        {
            this.eventBus = eventBus;
            this.wearablesProvider = wearablesProvider;
            this.emoteProvider = emoteProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.loadThumbnailCommand = loadThumbnailCommand;
            this.wearableStylingCatalog = wearableStylingCatalog;
            this.pendingTransferService = pendingTransferService;
            this.equippedStatusProvider = equippedStatusProvider;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
        }
        
        public IGiftingGridPresenter CreateWearablesPresenter(GiftingGridView view, SuperScrollGridAdapter<WearableViewModel> adapter)
        {
            adapter.UseWearableStyling(wearableStylingCatalog);

            return new WearableGridPresenter(
                view,
                adapter,
                eventBus,
                loadThumbnailCommand,
                equippedStatusProvider,
                pendingTransferService,
                wearablesProvider,
                wearableStylingCatalog,
                wearableStorage,
                emoteStorage);
        }

        public IGiftingGridPresenter CreateEmotesPresenter(GiftingGridView view, SuperScrollGridAdapter<EmoteViewModel> adapter)
        {
            adapter.UseWearableStyling(wearableStylingCatalog);

            return new EmoteGridPresenter(
                view,
                adapter,
                eventBus,
                loadThumbnailCommand,
                equippedStatusProvider,
                pendingTransferService,
                emoteProvider,
                web3IdentityCache,
                wearableStylingCatalog,
                wearableStorage,
                emoteStorage);
        }
    }
}