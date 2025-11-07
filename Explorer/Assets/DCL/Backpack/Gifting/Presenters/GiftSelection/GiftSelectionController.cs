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
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftSelectionController : ControllerBase<GiftingView, GiftingParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;
        
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        private readonly IGiftingGridPresenterFactory gridFactory;
        private readonly SendGiftCommand sendGiftCommand;
        private readonly IMVCManager mvcManager;
        
        private GiftingHeaderPresenter? headerPresenter;
        private GiftingFooterPresenter? footerPresenter;

        private GiftingTabsManager tabsManager;
        
        private IGiftingGridPresenter? wearablesGridPresenter;
        private IGiftingGridPresenter? emotesGridPresenter;
        private GiftingErrorsController? giftingErrorsController;

        private CancellationTokenSource? lifeCts;

        public GiftSelectionController(ViewFactoryMethod viewFactory,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository,
            IInputBlock inputBlock,
            IGiftingGridPresenterFactory gridFactory,
            IMVCManager mvcManager) : base(viewFactory)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock  = inputBlock;
            this.gridFactory = gridFactory;
            this.mvcManager = mvcManager;
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
                var emotesAdapter = new SuperScrollGridAdapter<EmoteViewModel>(viewInstance.EmotesGrid?.Grid);
                
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

                tabsManager.OnSectionDeactivated += HandleSectionDeactivated;
                tabsManager.OnSectionActivated   += HandleSectionActivated;
            }
        }

        private void HandleSectionDeactivated(GiftingSection section, IGiftingGridPresenter _)
        {
            headerPresenter?.ClearSearchImmediate();
        }

        private void HandleSectionActivated(GiftingSection section, IGiftingGridPresenter presenter)
        {
            //presenter.ForceSearch(string.Empty);
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

            tabsManager.OnSectionDeactivated -= HandleSectionDeactivated;
            tabsManager.OnSectionActivated -= HandleSectionActivated;
            
            giftingErrorsController!.Hide(true);
            lifeCts.SafeCancelAndDispose();
        }

        private void OnSelectionChanged(string? selectedUrn)
        {
            if (string.IsNullOrEmpty(selectedUrn))
            {
                footerPresenter?.UpdateState(null);
                return;
            }

            var active = tabsManager.ActivePresenter;
            string? itemName = active?.GetItemNameByUrn(selectedUrn);
            footerPresenter?.UpdateState(itemName);
        }

        private void HandleSendGift()
        {
            string? selectedUrn = tabsManager.ActivePresenter?.SelectedUrn;
            OpenTransferPopup().Forget();
        }

        private async UniTaskVoid OpenTransferPopup()
        {
            var active = tabsManager.ActivePresenter;
            string? urn    = active?.SelectedUrn;
            if (string.IsNullOrEmpty(urn))
                return;

            string recipientUserId = inputData.userId;
            string recipientName = inputData.userName;
            var userThumb = headerPresenter?.CurrentRecipientAvatarSprite; // or null if you don’t keep it

            // selected gift data
            string giftDisplayName = active!.GetItemNameByUrn(urn) ?? "Item";
            var giftThumb = active.GetThumbnailByUrn(urn);

            if (!active.TryBuildStyleSnapshot(urn, out var style))
                style = new GiftItemStyleSnapshot(null, null, Color.white);

            var data = new GiftTransferStatusParams(recipientUserId,
                recipientName,
                userThumb,
                urn,
                giftDisplayName,
                giftThumb,
                style
            );

            await mvcManager.ShowAsync(GiftTransferController.IssueCommand(data));
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
                viewInstance!.FooterView.SendGiftButton.OnClickAsync(ct),
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