using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Services.GiftingInventory;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.Passport;
using DCL.Profiles;
using MVC;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftSelectionController : ControllerBase<GiftingView, GiftSelectionParams>
    {
        private const string MsgErrorNonTransferableNft = "Cannot send this item as a gift because it's not a transferable NFT.";
        private const string CouldNotFindTokenForUrnLog = "Could not find a valid tokenId for URN {0}. Aborting gift transfer.";
        private const string DefaultGiftItemName = "Item";
        
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;
        
        private readonly IProfileRepository profileRepository;
        private readonly GiftSelectionComponentFactory componentFactory;
        private readonly GiftInventoryService giftInventoryService;
        private readonly IAvatarEquippedStatusProvider  equippedStatusProvider;
        private readonly IMVCManager mvcManager;
        
        private GiftingHeaderPresenter? headerPresenter;
        private GiftingFooterPresenter? footerPresenter;

        private GiftingTabsManager? tabsManager;
        
        private IGiftingGridPresenter? wearablesGridPresenter;
        private IGiftingGridPresenter? emotesGridPresenter;
        private GiftingErrorsController? giftingErrorsController;

        private CancellationTokenSource? lifeCts;

        public GiftSelectionController(ViewFactoryMethod viewFactory,
            GiftSelectionComponentFactory componentFactory,
            GiftInventoryService giftInventoryService,
            IAvatarEquippedStatusProvider  equippedStatusProvider,
            IProfileRepository profileRepository,
            IMVCManager mvcManager) : base(viewFactory)
        {
            this.componentFactory = componentFactory;
            this.giftInventoryService = giftInventoryService;
            this.equippedStatusProvider = equippedStatusProvider;
            this.profileRepository = profileRepository;
            this.mvcManager = mvcManager;
        }

        #region MVC
        
        protected override void OnViewInstantiated()
        {
            if (viewInstance == null) return;

            headerPresenter = componentFactory.CreateHeader(viewInstance.HeaderView);
            footerPresenter = componentFactory.CreateFooter(viewInstance.FooterView);
            giftingErrorsController = componentFactory.CreateErrorController(viewInstance);
            
            tabsManager = componentFactory.CreateTabs(viewInstance,
                out var wPresenter,
                out var ePresenter);

            wearablesGridPresenter = wPresenter;
            emotesGridPresenter = ePresenter;
        }

        protected override void OnViewShow()
        {
            InitializeViewAsync()
                .Forget();
        }

        private async UniTask InitializeViewAsync()
        {
            lifeCts = new CancellationTokenSource();
            giftingErrorsController?.Hide(true);

            try
            {
                await equippedStatusProvider.InitializeAsync(lifeCts.Token);
                if (lifeCts.IsCancellationRequested) return;

                headerPresenter?.ClearSearchImmediate();
                wearablesGridPresenter?.Deactivate();
                emotesGridPresenter?.Deactivate();
                footerPresenter?.SetInitialState();

                headerPresenter?.SetupAsync(inputData.userAddress, inputData.userName, lifeCts.Token).Forget();

                if (headerPresenter != null) headerPresenter.OnSearchChanged += HandleSearchChanged;
                if (footerPresenter != null) footerPresenter.OnSendGift += HandleSendGift;

                if (wearablesGridPresenter != null)
                {
                    wearablesGridPresenter.OnSelectionChanged += OnSelectionChanged;
                    wearablesGridPresenter.OnLoadingStateChanged += OnLoadingStateChanged;
                }

                if (emotesGridPresenter != null)
                {
                    emotesGridPresenter.OnSelectionChanged += OnSelectionChanged;
                    emotesGridPresenter.OnLoadingStateChanged += OnLoadingStateChanged;
                }

                if (tabsManager != null)
                {
                    tabsManager.OnSectionDeactivated += HandleSectionDeactivated;
                    tabsManager.Initialize();
                }

                wearablesGridPresenter?.SetSearchText(string.Empty);
                emotesGridPresenter?.SetSearchText(string.Empty);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
            }
        }

        private void HandleSectionDeactivated(GiftingSection section, IGiftingGridPresenter _)
        {
            headerPresenter?.ClearSearchImmediate();
        }

        protected override void OnViewClose()
        {
            if (headerPresenter != null) headerPresenter.OnSearchChanged -= HandleSearchChanged;
            if (footerPresenter != null) footerPresenter.OnSendGift -= HandleSendGift;

            if (wearablesGridPresenter != null)
            {
                wearablesGridPresenter.OnSelectionChanged -= OnSelectionChanged;
                wearablesGridPresenter.OnLoadingStateChanged -= OnLoadingStateChanged;
            }

            if (emotesGridPresenter != null)
            {
                emotesGridPresenter.OnSelectionChanged -= OnSelectionChanged;
                emotesGridPresenter.OnLoadingStateChanged -= OnLoadingStateChanged;
            }

            if (tabsManager != null)
                tabsManager.OnSectionDeactivated -= HandleSectionDeactivated;

            wearablesGridPresenter?.Deactivate();
            emotesGridPresenter?.Deactivate();

            giftingErrorsController?.Hide(true);
            lifeCts.SafeCancelAndDispose();
        }

        private void OnSelectionChanged(string? selectedUrn)
        {
            if (string.IsNullOrEmpty(selectedUrn))
            {
                footerPresenter?.UpdateState(null);
                return;
            }

            var active = tabsManager?.ActivePresenter;
            string? itemName = active?.GetItemNameByUrn(selectedUrn);
            footerPresenter?.UpdateState(itemName, inputData.userName);
        }

        private void OnLoadingStateChanged(bool isLoading)
        {
            if (viewInstance == null || tabsManager == null)
                return;

            var activePresenter = tabsManager.ActivePresenter;
            if (activePresenter == null)
                return;

            if (isLoading)
            {
                if (activePresenter.CurrentItemCount == 0)
                    viewInstance.ProgressContainer.SetActive(true);
            }
            else
            {
                viewInstance.ProgressContainer.SetActive(false);
            }
        }

        private void HandleSendGift()
        {
            OpenTransferPopupAsync()
                .Forget();
        }

        private async UniTask OpenTransferPopupAsync()
        {
            var activePresenter = tabsManager?.ActivePresenter;
            string? selectedUrn = activePresenter?.SelectedUrn;

            if (activePresenter == null || string.IsNullOrEmpty(selectedUrn))
                return;

            try
            {
                string itemType = activePresenter is WearableGridPresenter
                    ? GiftingItemTypes.Wearable
                    : GiftingItemTypes.Emote;

                if (!giftInventoryService.TryGetBestTransferableToken(new URN(selectedUrn), itemType, out string tokenId, out string instanceUrn))
                {
                    ReportHub.LogError( ReportCategory.GIFTING, string.Format(CouldNotFindTokenForUrnLog, selectedUrn));
                    giftingErrorsController?.Show(MsgErrorNonTransferableNft);
                    return;
                }

                string giftDisplayName = activePresenter.GetItemNameByUrn(selectedUrn) ?? DefaultGiftItemName;
                var giftThumb = activePresenter.GetThumbnailByUrn(selectedUrn);

                if (!activePresenter.TryBuildStyleSnapshot(selectedUrn, out var style))
                    style = new GiftItemStyleSnapshot(null, null, Color.white);

                var ct = lifeCts?.Token ?? CancellationToken.None;
                var recipientProfile = await profileRepository.GetAsync(inputData.userAddress, ct);
                if (ct.IsCancellationRequested) return;

                var userNameColor = recipientProfile?.UserNameColor ?? Color.black;
                string userNameColorHex = ColorUtility.ToHtmlStringRGB(userNameColor);

                var transferParams = new GiftTransferParams(
                    inputData.userAddress,
                    inputData.userName,
                    headerPresenter?.CurrentRecipientAvatarSprite,
                    selectedUrn,
                    giftDisplayName,
                    giftThumb,
                    style,
                    itemType,
                    tokenId,
                    instanceUrn,
                    userNameColorHex
                );

                await mvcManager.ShowAsync(GiftTransferController.IssueCommand(transferParams), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // expected path when the popup / controller is disposed; no-op
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
            }
        }

        private void HandleSearchChanged(string searchText)
        {
            tabsManager?.ActivePresenter?.SetSearchText(searchText);
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