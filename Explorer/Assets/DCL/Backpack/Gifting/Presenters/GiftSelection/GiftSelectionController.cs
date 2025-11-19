using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Cache;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Passport;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Profiles.Helpers;
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
        private readonly IGiftingGridPresenterFactory gridFactory;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        
        private GiftingHeaderPresenter? headerPresenter;
        private GiftingFooterPresenter? footerPresenter;

        private GiftingTabsManager tabsManager;
        
        private IGiftingGridPresenter? wearablesGridPresenter;
        private IGiftingGridPresenter? emotesGridPresenter;
        private GiftingErrorsController? giftingErrorsController;

        private CancellationTokenSource? lifeCts;
        private EquippedItemContext equippedItemsContext;

        public GiftSelectionController(ViewFactoryMethod viewFactory,
            GiftSelectionComponentFactory componentFactory,
            IPendingTransferService pendingTransferService,
            IAvatarEquippedStatusProvider  equippedStatusProvider,
            IProfileRepository profileRepository,
            IGiftingGridPresenterFactory gridFactory,
            IMVCManager mvcManager,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            ISelfProfile selfProfile) : base(viewFactory)
        {
            this.componentFactory = componentFactory;
            this.pendingTransferService = pendingTransferService;
            this.equippedStatusProvider = equippedStatusProvider;
            this.gridFactory = gridFactory;
            this.profileRepository = profileRepository;
            this.mvcManager = mvcManager;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.selfProfile = selfProfile;
        }

        #region MVC


        protected override void OnViewInstantiated()
        {
            if (viewInstance == null) return;

            var wearablesAdapter = new SuperScrollGridAdapter<WearableViewModel>(viewInstance.WearablesGrid?.Grid);
            var emotesAdapter = new SuperScrollGridAdapter<EmoteViewModel>(viewInstance.EmotesGrid?.Grid);

            headerPresenter = componentFactory.CreateHeader(viewInstance.HeaderView);
            footerPresenter = componentFactory.CreateFooter(viewInstance.FooterView);
            giftingErrorsController = componentFactory.CreateErrorController(viewInstance);

            wearablesGridPresenter = gridFactory.CreateWearablesPresenter(viewInstance?.WearablesGrid, wearablesAdapter);
            emotesGridPresenter = gridFactory.CreateEmotesPresenter(viewInstance?.EmotesGrid, emotesAdapter);

            tabsManager = componentFactory.CreateTabs(viewInstance,
                wearablesGridPresenter,
                emotesGridPresenter);

            tabsManager.OnSectionDeactivated += HandleSectionDeactivated;
            tabsManager.OnSectionActivated   += HandleSectionActivated;
        }

        protected override async void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();
            viewInstance!.ErrorNotification.Hide(true);

            //await equippedStatusProvider.InitializeAsync(lifeCts.Token);
            await CreateEquippedContextAsync(lifeCts.Token);
            if (lifeCts.IsCancellationRequested) return;

            // pendingTransferService.Prune( wearableStorage.AllOwnedNftRegistry,
            //     emoteStorage.AllOwnedNftRegistry);

            PendingGiftsCache.Prune(wearableStorage.AllOwnedNftRegistry, emoteStorage.AllOwnedNftRegistry);

            wearablesGridPresenter?.PrepareForLoading(equippedItemsContext);
            emotesGridPresenter?.PrepareForLoading(equippedItemsContext);

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

            // lifeCts = new CancellationTokenSource();
            // giftingErrorsController?.Hide(true);
            //
            // try
            // {
            //     // 1. Initialize Equipped Status
            //     await equippedStatusProvider.InitializeAsync(lifeCts.Token);
            //     if (lifeCts.IsCancellationRequested) return;
            //
            //     // 2. Prune Pending Transfers (using injected storage data)
            //     pendingTransferService.Prune(
            //         wearableStorage.AllOwnedNftRegistry,
            //         emoteStorage.AllOwnedNftRegistry);
            //
            //     // 3. Setup UI
            //     headerPresenter?.ClearSearchImmediate();
            //
            //     // Note: Presenters now have dependencies injected, no need for manual PrepareForLoading
            //     tabsManager?.ActivePresenter?.Deactivate();
            //
            //     headerPresenter?.SetupAsync(inputData.userAddress, inputData.userName, lifeCts.Token).Forget();
            //     footerPresenter?.SetInitialState();
            //
            //     // 4. Wire Events
            //     if (headerPresenter != null) headerPresenter.OnSearchChanged += HandleSearchChanged;
            //     if (footerPresenter != null) footerPresenter.OnSendGift += HandleSendGift;
            //
            //     // Note: Events on grid presenters are handled inside TabsManager or we iterate them here if needed
            //     // Ideally TabsManager exposes "OnActiveSelectionChanged" to avoid iterating specific presenters.
            //     // For now, keeping your logic:
            //     if (tabsManager?.ActivePresenter != null)
            //     {
            //         // You might need to subscribe to all presenters created in factory
            //         // Or better, expose an event on TabsManager that aggregates them.
            //     }
            //
            //     tabsManager?.Initialize();
            // }
            // catch (Exception e)
            // {
            //     ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
            // }
        }

        private void HandleSectionDeactivated(GiftingSection section, IGiftingGridPresenter _)
        {
            headerPresenter?.ClearSearchImmediate();
        }

        private void HandleSectionActivated(GiftingSection section, IGiftingGridPresenter presenter)
        {
            //presenter.ForceSearch(string.Empty);
        }

        private async UniTask CreateEquippedContextAsync(CancellationToken ct)
        {
            equippedItemsContext = new EquippedItemContext();
            var profile = await selfProfile.ProfileAsync(ct);
            if (profile != null)
                equippedItemsContext.Populate(profile.Avatar);
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
                // IMPORTANT: entry.Urn here is the *instance* URN
                var entryUrn = new URN(entry.Urn);

                if (!equippedItemsContext.IsSpecificInstanceEquipped(entryUrn) &&
                    !PendingGiftsCache.Contains(entryUrn))
                {
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
        
        /// <summary>
        /// Finds the newest, unequipped NFT entry from a collection of owned copies.
        /// </summary>
        /// <param name="ownedCopies">The dictionary of all owned instances for a single item type.</param>
        /// <param name="bestEntry">The best unequipped entry found.</param>
        /// <returns>True if an unequipped copy was found.</returns>
        private bool TryFindBestUnequippedNft(
            IReadOnlyDictionary<URN, NftBlockchainOperationEntry> ownedCopies,
            out NftBlockchainOperationEntry bestEntry)
        {
            bestEntry = default;
            bool foundCandidate = false;

            // Loop through all owned copies of the item
            foreach (var currentEntry in ownedCopies.Values)
            {
                // The core logic: check if this specific instance is NOT equipped
                if (!equippedItemsContext.IsSpecificInstanceEquipped(currentEntry.Urn))
                {
                    // It's a valid, giftable candidate. Is it the newest one we've found so far?
                    if (!foundCandidate || currentEntry.TransferredAt > bestEntry.TransferredAt)
                    {
                        bestEntry = currentEntry;
                        foundCandidate = true;
                    }
                }
            }

            return foundCandidate;
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