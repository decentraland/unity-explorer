using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Factory;
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
        private readonly IPendingTransferService pendingTransferService;
        private readonly IAvatarEquippedStatusProvider  equippedStatusProvider;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;
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
            IPendingTransferService pendingTransferService,
            IAvatarEquippedStatusProvider  equippedStatusProvider,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage) : base(viewFactory)
        {
            this.componentFactory = componentFactory;
            this.pendingTransferService = pendingTransferService;
            this.equippedStatusProvider = equippedStatusProvider;
            this.profileRepository = profileRepository;
            this.mvcManager = mvcManager;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
        }

        #region MVC
        
        protected override void OnViewInstantiated()
        {
            if (viewInstance == null) return;

            headerPresenter = componentFactory.CreateHeader(viewInstance.HeaderView);
            footerPresenter = componentFactory.CreateFooter(viewInstance.FooterView);
            giftingErrorsController = componentFactory.CreateErrorController(viewInstance);

            // Create presenters and capture references
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
            var activePresenter = tabsManager.ActivePresenter;
            string selectedUrn = activePresenter?.SelectedUrn;

            if (string.IsNullOrEmpty(selectedUrn)) return;

            string itemType = activePresenter is WearableGridPresenter ? "wearable" : "emote";

            if (!TryGetGiftTokenId(new URN(selectedUrn), itemType, out string tokenId, out var instanceUrn))
            {
                ReportHub.LogError(ReportCategory.GIFTING, $"Could not find a valid tokenId for URN {selectedUrn}. Aborting gift transfer.");
                giftingErrorsController.Show("Cannot send this item as a gift because it's not a transferable NFT.");
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

        private bool TryGetGiftTokenId(
            URN itemBaseUrn,
            string itemType,
            out string tokenId,
            out URN instanceUrn)
        {
            tokenId = "0";
            instanceUrn = default;

            IReadOnlyDictionary<URN, NftBlockchainOperationEntry> ownedCopies;

            // 1. Get the dictionary of instances (TokenID -> Entry) for this specific Base URN
            if (itemType == "wearable")
            {
                if (!wearableStorage.TryGetOwnedNftRegistry(itemBaseUrn, out ownedCopies))
                    return false;
            }
            else
            {
                if (!emoteStorage.TryGetOwnedNftRegistry(itemBaseUrn, out ownedCopies))
                    return false;
            }

            NftBlockchainOperationEntry? bestCandidate = null;

            foreach (var entry in ownedCopies.Values)
            {
                // entry.Urn is the Full URN (e.g. urn:...:tokenId)
                string fullUrnString = entry.Urn;

                // Check dependencies: Is it currently on avatar? Is it currently pending?
                if (!equippedStatusProvider.IsEquipped(fullUrnString) &&
                    !pendingTransferService.IsPending(fullUrnString))
                {
                    // Pick the most recently transferred/acquired one
                    if (bestCandidate == null || entry.TransferredAt > bestCandidate.Value.TransferredAt)
                        bestCandidate = entry;
                }
            }

            if (bestCandidate != null)
            {
                tokenId = bestCandidate.Value.TokenId;
                instanceUrn = new URN(bestCandidate.Value.Urn);
                return true;
            }

            return false;
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