using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Services.GiftingInventory;
using DCL.Backpack.Gifting.Services.PendingTransfers;
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
            InitializeViewAsync().Forget();
        }

        private async UniTaskVoid InitializeViewAsync()
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
                    tabsManager.OnSectionActivated += HandleSectionActivated;

                    tabsManager.Initialize();
                }
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

        private void HandleSectionActivated(GiftingSection section, IGiftingGridPresenter presenter)
        {
            //presenter.ForceSearch(string.Empty);
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
            {
                tabsManager.OnSectionDeactivated -= HandleSectionDeactivated;
                tabsManager.OnSectionActivated -= HandleSectionActivated;
            }

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

            var active = tabsManager.ActivePresenter;
            string? itemName = active?.GetItemNameByUrn(selectedUrn);
            footerPresenter?.UpdateState(itemName, inputData.userName);
        }

        private void OnLoadingStateChanged(bool isLoading)
        {
            if (viewInstance == null) return;

            var activePresenter = tabsManager.ActivePresenter;
            if (activePresenter == null) return;

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
            OpenTransferPopupAsync().Forget();
        }
        
        private async UniTaskVoid OpenTransferPopupAsync()
        {
            var activePresenter = tabsManager?.ActivePresenter;
            string selectedUrn = activePresenter?.SelectedUrn;

            if (string.IsNullOrEmpty(selectedUrn)) return;

            string itemType = activePresenter is WearableGridPresenter ? "wearable" : "emote";

            if (!giftInventoryService.TryGetBestTransferableToken(new URN(selectedUrn), itemType, out string tokenId, out var instanceUrn))
            {
                ReportHub.LogError(ReportCategory.GIFTING, $"Could not find a valid tokenId for URN {selectedUrn}. Aborting gift transfer.");
                giftingErrorsController?.Show("Cannot send this item as a gift because it's not a transferable NFT.");
                return;
            }

            string giftDisplayName = activePresenter?.GetItemNameByUrn(selectedUrn) ?? "Item";
            var giftThumb = activePresenter?.GetThumbnailByUrn(selectedUrn);
            if (!activePresenter.TryBuildStyleSnapshot(selectedUrn, out var style))
                style = new GiftItemStyleSnapshot(null, null, Color.white);

            var recipientProfile = await profileRepository.GetAsync(inputData.userAddress, CancellationToken.None);

            string? userNameColorHex = "000000";
            if (recipientProfile != null)
                userNameColorHex = ColorUtility.ToHtmlStringRGB(recipientProfile.UserNameColor);
            
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
                instanceUrn.ToString(),
                userNameColorHex
            );

            await mvcManager.ShowAsync(GiftTransferController.IssueCommand(transferParams));
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