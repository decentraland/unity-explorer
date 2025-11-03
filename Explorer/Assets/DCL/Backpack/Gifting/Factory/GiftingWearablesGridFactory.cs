using System;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Presenters.Grid;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Views;
using Utility;

namespace DCL.Backpack.Gifting.Factory
{
    public interface IGiftingGridPresenterFactory
    {
        IGiftingGridPresenter CreateWearablesPresenter(GiftingGridView view, SuperScrollGridAdapter<WearableViewModel> adapter);
        IGiftingGridPresenter CreateEmotesPresenter(GiftingGridView view, SuperScrollGridAdapter<WearableViewModel> adapter);
    }

    public sealed class GiftingGridPresenterFactory : IGiftingGridPresenterFactory
    {
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEventBus eventBus;
        private readonly LoadGiftableItemThumbnailCommand loadThumbnailCommand;

        public GiftingGridPresenterFactory(
            IWearablesProvider wearablesProvider,
            IEventBus eventBus,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand)
        {
            this.wearablesProvider = wearablesProvider;
            this.eventBus = eventBus;
            this.loadThumbnailCommand = loadThumbnailCommand;
        }


        public IGiftingGridPresenter CreateWearablesPresenter(GiftingGridView view, SuperScrollGridAdapter<WearableViewModel> adapter)
        {
            return new WearableGridPresenter(view, adapter, wearablesProvider, eventBus, loadThumbnailCommand);
        }

        public IGiftingGridPresenter CreateEmotesPresenter(GiftingGridView view, SuperScrollGridAdapter<WearableViewModel> adapter)
        {
            // throw new  NotImplementedException();
            return new EmoteGridPresenter(view, adapter, wearablesProvider, eventBus, loadThumbnailCommand);
        }
    }
}