using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Views;
using DCL.Input;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

namespace DCL.Backpack.Gifting.Factory
{
    public class GiftSelectionComponentFactory
    {
        private readonly IProfileRepository profileRepo;
        private readonly ProfileRepositoryWrapper profileRepoWrapper;
        private readonly IInputBlock inputBlock;
        private readonly IGiftingGridPresenterFactory gridFactory;

        public GiftSelectionComponentFactory(IProfileRepository profileRepo,
            ProfileRepositoryWrapper profileRepoWrapper,
            IInputBlock inputBlock,
            IGiftingGridPresenterFactory gridFactory)
        {
            this.profileRepo = profileRepo;
            this.profileRepoWrapper = profileRepoWrapper;
            this.inputBlock = inputBlock;
            this.gridFactory = gridFactory;
        }

        public GiftingHeaderPresenter CreateHeader(GiftingHeaderView view)
        {
            return new GiftingHeaderPresenter(view,
                profileRepo,
                profileRepoWrapper,
                inputBlock);
        }

        public GiftingFooterPresenter CreateFooter(GiftingFooterView view)
        {
            return new GiftingFooterPresenter(view);
        }

        public GiftingErrorsController CreateErrorController(GiftingView view)
        {
            return new GiftingErrorsController(view.ErrorNotification);
        }

        public GiftingTabsManager CreateTabs(GiftingView view,
            IGiftingGridPresenter wearables,
            IGiftingGridPresenter emotes)
        {
            return new GiftingTabsManager(view, wearables, emotes);
        }

        // public GiftingTabsManager CreateTabs(GiftingView view,
        //     out IGiftingGridPresenter wearables, 
        //     out IGiftingGridPresenter emotes)
        // {
        //     var wAdapter = new SuperScrollGridAdapter<WearableViewModel>(view.WearablesGrid?.Grid);
        //     var eAdapter = new SuperScrollGridAdapter<EmoteViewModel>(view.EmotesGrid?.Grid);
        //
        //     wearables = gridFactory.CreateWearablesPresenter(view.WearablesGrid, wAdapter);
        //     emotes = gridFactory.CreateEmotesPresenter(view.EmotesGrid, eAdapter);
        //
        //     return new GiftingTabsManager(view, wearables, emotes);
        // }
    }
}