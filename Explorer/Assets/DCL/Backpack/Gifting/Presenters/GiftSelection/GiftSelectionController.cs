using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftSelectionController : ControllerBase<GiftingView, GiftSelectionParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;
        
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;
        private readonly IInputBlock inputBlock;
        private readonly IGiftingGridPresenterFactory gridFactory;
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
            IMVCManager mvcManager,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage) : base(viewFactory)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock  = inputBlock;
            this.gridFactory = gridFactory;
            this.mvcManager = mvcManager;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
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

            headerPresenter?.SetupAsync(inputData.userAddress,
                inputData.userName,
                lifeCts.Token).Forget();
            
            footerPresenter?.SetInitialState();

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

            tabsManager.Initialize();
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

            // Determine the item type and try to get a valid, unique tokenId for the transaction.
            string itemType = activePresenter is WearableGridPresenter ? "wearable" : "emote";
            if (!TryGetGiftTokenId(new URN(selectedUrn), itemType, out string tokenId))
            {
                ReportHub.LogError(ReportCategory.GIFTING, $"Could not find a valid tokenId for URN {selectedUrn}. Aborting gift transfer.");
                giftingErrorsController.Show("Cannot send this item as a gift because it's not a transferable NFT.");
                return;
            }

            // Gather all remaining data for the transfer popup
            string giftDisplayName = activePresenter.GetItemNameByUrn(selectedUrn) ?? "Item";
            var giftThumb = activePresenter.GetThumbnailByUrn(selectedUrn);
            if (!activePresenter.TryBuildStyleSnapshot(selectedUrn, out var style))
                style = new GiftItemStyleSnapshot(null, null, Color.white);

            var transferParams = new GiftTransferParams(
                inputData.userAddress,
                inputData.userName,
                headerPresenter.CurrentRecipientAvatarSprite,
                selectedUrn,
                giftDisplayName,
                giftThumb,
                style,
                itemType,
                tokenId
            );

            await mvcManager.ShowAsync(GiftTransferController.IssueCommand(transferParams));
        }

        /// <summary>
        ///     Retrieves the latest owned unique Token ID for a given NFT URN.
        ///     This is necessary to identify which specific copy of an item to transfer.
        /// </summary>
        /// <returns>True if a valid, on-chain token ID was found.</returns>
        private bool TryGetGiftTokenId(URN itemUrn, string itemType, out string tokenId)
        {
            tokenId = "0";

            switch (itemType)
            {
                case "wearable" when wearableStorage.TryGetLatestOwnedNft(itemUrn, out var wearableEntry):
                    tokenId = wearableEntry.TokenId;
                    return true;
                case "emote" when emoteStorage.TryGetLatestOwnedNft(itemUrn, out var emoteEntry):
                    tokenId = emoteEntry.TokenId;
                    return true;
                default:
                    return false;
            }
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