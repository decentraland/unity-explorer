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
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileWrapper;
        private readonly IInputBlock inputBlock;
        private readonly IGiftingGridPresenterFactory gridFactory;

        public GiftSelectionComponentFactory(
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper profileWrapper,
            IInputBlock inputBlock,
            IGiftingGridPresenterFactory gridFactory)
        {
            this.profileRepository = profileRepository;
            this.profileWrapper = profileWrapper;
            this.inputBlock = inputBlock;
            this.gridFactory = gridFactory;
        }

        public GiftingHeaderPresenter CreateHeader(GiftingHeaderView view)
        {
            return new GiftingHeaderPresenter(view, profileRepository, profileWrapper, inputBlock);
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
            out IGiftingGridPresenter wearablesPresenter,
            out IGiftingGridPresenter emotesPresenter)
        {
            var wAdapter = new SuperScrollGridAdapter<WearableViewModel>(view.WearablesGrid?.Grid);
            var eAdapter = new SuperScrollGridAdapter<EmoteViewModel>(view.EmotesGrid?.Grid);

            wearablesPresenter = gridFactory.CreateWearablesPresenter(view.WearablesGrid, wAdapter);
            emotesPresenter = gridFactory.CreateEmotesPresenter(view.EmotesGrid, eAdapter);

            return new GiftingTabsManager(view, wearablesPresenter, emotesPresenter);
        }
    }
}