using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Views;
using DCL.Input;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingController : ControllerBase<GiftingView, GiftingParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;
        
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        private readonly IGiftingGridPresenterFactory gridFactory;
        private readonly SendGiftCommand sendGiftCommand;
        
        private GiftingHeaderPresenter? headerPresenter;
        private GiftingFooterPresenter? footerPresenter;

        private GiftingTabsManager tabsManager;
        
        private IGiftingGridPresenter? wearablesGridPresenter;
        private IGiftingGridPresenter? emotesGridPresenter;
        private GiftingErrorsController? giftingErrorsController;
        
        private CancellationTokenSource? lifeCts;

        // TODO: erase this one
        private UniTask<IGiftingGridPresenter>? wearablesGridBuildTask;

        public GiftingController(ViewFactoryMethod viewFactory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository,
            IInputBlock inputBlock,
            IGiftingGridPresenterFactory gridFactory,
            SendGiftCommand sendGiftCommand) : base(viewFactory)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock  = inputBlock;
            this.gridFactory = gridFactory;
            this.sendGiftCommand = sendGiftCommand;
        }
        
        private void OnPublishError()
        {
            giftingErrorsController!.Show();
        }
        
        #region MVC

        
        protected override void OnViewInstantiated()
        {
            if (viewInstance != null)
            {
                var wearablesAdapter = new SuperScrollGridAdapter<WearableViewModel>(viewInstance.WearablesGrid?.Grid);
                var emotesAdapter = new SuperScrollGridAdapter<WearableViewModel>(viewInstance.EmotesGrid?.Grid);
                
                headerPresenter = new GiftingHeaderPresenter(viewInstance.HeaderView,
                    profileRepository,
                    profileRepositoryWrapper,
                    inputBlock);

                footerPresenter = new GiftingFooterPresenter(viewInstance!.FooterView);

                wearablesGridPresenter = gridFactory.CreateWearablesPresenter(viewInstance?.WearablesGrid, wearablesAdapter);
                emotesGridPresenter = gridFactory.CreateEmotesPresenter(viewInstance?.EmotesGrid, emotesAdapter);
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

            headerPresenter?.ClearSearchImmediate();
            wearablesGridPresenter?.Deactivate();
            emotesGridPresenter?.Deactivate();
            
            headerPresenter?.SetupAsync(inputData.userId,
                inputData.userName,
                lifeCts.Token).Forget();
            
            footerPresenter?.SetInitialState();

            if (headerPresenter != null) headerPresenter.OnSearchChanged += HandleSearchChanged;
            if (footerPresenter != null) footerPresenter.OnSendGift += HandleSendGift;
            if (wearablesGridPresenter != null) wearablesGridPresenter.OnSelectionChanged += OnSelectionChanged;
            if (emotesGridPresenter != null) emotesGridPresenter.OnSelectionChanged += OnSelectionChanged;

            tabsManager.Initialize();
        }

        protected override void OnViewClose()
        {
            if (headerPresenter != null) headerPresenter.OnSearchChanged -= HandleSearchChanged;
            if (footerPresenter != null) footerPresenter.OnSendGift -= HandleSendGift;
            if (wearablesGridPresenter != null) wearablesGridPresenter.OnSelectionChanged -= OnSelectionChanged;
            if (emotesGridPresenter != null) emotesGridPresenter.OnSelectionChanged -= OnSelectionChanged;

            wearablesGridPresenter?.Deactivate();
            emotesGridPresenter?.Deactivate();
            
            giftingErrorsController!.Hide(true);
            lifeCts.SafeCancelAndDispose();
        }

        private void OnSelectionChanged(string? selectedUrn)
        {
            if (string.IsNullOrEmpty(selectedUrn))
            {
                // Nothing is selected, update footer to its default state.
                footerPresenter?.UpdateState(null);
            }
            else
            {
                // Something is selected. Get the item name from the active presenter and update the footer.
                // Note: This requires adding a method to IGiftingGridPresenter
                var activePresenter = tabsManager.ActivePresenter;
                if (activePresenter is WearableGridPresenter wearablePresenter) // Example for wearables
                {
                    string? itemName = wearablePresenter.GetItemNameByUrn(selectedUrn);
                    footerPresenter?.UpdateState(itemName);
                }
            }
        }

        private void HandleSendGift()
        {
            string? selectedUrn = tabsManager.ActivePresenter?.SelectedUrn;
            sendGiftCommand.ExecuteAsync(inputData.userId, selectedUrn).Forget();
        }

        private void HandleSearchChanged(string searchText)
        {
            tabsManager.ActivePresenter?.SetSearchText(searchText);
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