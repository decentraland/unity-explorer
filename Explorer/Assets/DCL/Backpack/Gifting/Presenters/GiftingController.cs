using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Input;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using UnityEngine.Assertions;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingController : ControllerBase<GiftingView, GiftingParams>
    {
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private GiftingHeaderPresenter? headerPresenter;
        private GiftingFooterPresenter? footerPresenter;
        private IGiftingGridPresenter? wearablesGridPresenter;
        private IGiftingGridPresenter? emotesGridPresenter;
        private GiftingTabsManager tabsManager;
        private GiftingErrorsController? giftingErrorsController;
        private CancellationTokenSource? lifeCts;

        public GiftingController(ViewFactoryMethod viewFactory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository,
            IInputBlock inputBlock) : base(viewFactory)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock  = inputBlock;
        }
        
        private void OnPublishError()
        {
            giftingErrorsController!.Show();
        }
        
        #region MVC

        protected override void OnViewInstantiated()
        {
            Assert.IsNotNull(viewInstance, "GiftingView prefab is null!");
            Assert.IsNotNull(viewInstance!.HeaderView, "HeaderView is not assigned in the GiftingView prefab!");
            Assert.IsNotNull(viewInstance!.FooterView, "FooterView is not assigned in the GiftingView prefab!");
            Assert.IsNotNull(viewInstance!.WearablesGrid, "WearablesGrid is not assigned in the GiftingView prefab!");
            Assert.IsNotNull(viewInstance!.EmotesGrid, "EmotesGrid is not assigned in the GiftingView prefab!");
            
            if (viewInstance != null)
            {
                headerPresenter = new GiftingHeaderPresenter(viewInstance.HeaderView,
                    profileRepository,
                    profileRepositoryWrapper,
                    inputBlock);

                footerPresenter = new GiftingFooterPresenter(viewInstance!.FooterView);

                wearablesGridPresenter = new PlaceholderGiftingGridPresenter(viewInstance!.WearablesGrid?.gameObject);
                emotesGridPresenter = new PlaceholderGiftingGridPresenter(viewInstance!.EmotesGrid?.gameObject);

                giftingErrorsController = new GiftingErrorsController(viewInstance!.ErrorNotification);

                tabsManager = new GiftingTabsManager(viewInstance,
                    wearablesGridPresenter,
                    emotesGridPresenter);
            }
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();
            viewInstance!.ErrorNotification.Hide(true);

            tabsManager?.Initialize();

            headerPresenter?.SetupAsync(inputData.userId, lifeCts.Token).Forget();
            footerPresenter?.SetInitialState();

            if (headerPresenter != null)
                headerPresenter.OnSearchChanged += HandleSearchChanged;

            if (footerPresenter != null)
                footerPresenter.OnSendGift += HandleSendGift;
        }

        protected override void OnViewClose()
        {
            if (headerPresenter != null)
                headerPresenter.OnSearchChanged -= HandleSearchChanged;

            if (footerPresenter != null)
                footerPresenter.OnSendGift -= HandleSendGift;

            giftingErrorsController!.Hide(true);

            lifeCts.SafeCancelAndDispose();
        }

        private void HandleSendGift()
        {
            // This method will be called when the Send Gift button is clicked.
            // IMPORTANT: Since this button does NOT close the view itself, its logic is handled here.
            // The view will only close when a button in WaitForCloseIntentAsync is clicked.

            // 1. Instantiate and run the SendGiftCommand
            //    var sendGiftCommand = new SendGiftCommand(...);
            //    sendGiftCommand.ExecuteAsync(...).Forget();

            // 2. Here, you would likely HIDE the current view and show the "Preparing Gift" view.
            //    The MVCManager handles this by showing the new popup, which will obscure the old one.
            //    mvcManager.ShowAsync(TransferProgressController.IssueCommand(...)).Forget();
        }

        private void HandleSearchChanged(string searchText)
        {
            wearablesGridPresenter?.SetSearchText(searchText);
            emotesGridPresenter?.SetSearchText(searchText);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return UniTask.Never(ct);
            
            return UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance!.BackgroundButton.OnClickAsync(ct),
                viewInstance!.FooterView.CancelButton.OnClickAsync(ct)
            );
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
            headerPresenter?.Dispose();
            footerPresenter?.Dispose();
            tabsManager?.Dispose();
        }
    }
}