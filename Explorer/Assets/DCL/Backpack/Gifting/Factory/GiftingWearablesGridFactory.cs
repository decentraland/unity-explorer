using System;
using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Presenters.Grid;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
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
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEmoteProvider emoteProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IReadOnlyCollection<URN> embeddedEmotes;
        private readonly IEventBus eventBus;
        private readonly LoadGiftableItemThumbnailCommand loadThumbnailCommand;
        private readonly IWearableStylingCatalog wearableStylingCatalog;

        public GiftingGridPresenterFactory(
            IEventBus eventBus,
            IWearablesProvider wearablesProvider,
            IEmoteProvider emoteProvider,
            IWeb3IdentityCache web3IdentityCache,
            IReadOnlyCollection<URN> embeddedEmotes,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand,
            IWearableStylingCatalog wearableStylingCatalog)
        {
            this.wearablesProvider = wearablesProvider;
            this.emoteProvider = emoteProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.embeddedEmotes = embeddedEmotes;
            this.eventBus = eventBus;
            this.loadThumbnailCommand = loadThumbnailCommand;
            this.wearableStylingCatalog  = wearableStylingCatalog;
        }
        
        public IGiftingGridPresenter CreateWearablesPresenter(GiftingGridView view, SuperScrollGridAdapter<WearableViewModel> adapter)
        {
            adapter.UseWearableStyling(wearableStylingCatalog);
            return new WearableGridPresenter(view,
                adapter,
                wearablesProvider,
                eventBus,
                loadThumbnailCommand,
                wearableStylingCatalog);
        }

        public IGiftingGridPresenter CreateEmotesPresenter(GiftingGridView view, SuperScrollGridAdapter<EmoteViewModel> adapter)
        {
            adapter.UseWearableStyling(wearableStylingCatalog);
            return new EmoteGridPresenter(view,
                adapter,
                emoteProvider,
                web3IdentityCache,
                embeddedEmotes,
                eventBus,
                loadThumbnailCommand,
                wearableStylingCatalog);
        }
    }
}