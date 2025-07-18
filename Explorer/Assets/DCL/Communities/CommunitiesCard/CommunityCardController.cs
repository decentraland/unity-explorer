using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Clipboard;
using DCL.Communities.CommunitiesCard.Events;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunityCreation;
using DCL.Communities.CommunitiesCard.Places;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Friends;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.PhotoDetail;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardController : ControllerBase<CommunityCardView, CommunityCardParameter>
    {
        private static readonly int BG_SHADER_COLOR_1 = Shader.PropertyToID("_Color1");

        private const string JOIN_COMMUNITY_ERROR_TEXT = "There was an error joining the community. Please try again.";
        private const string DELETE_COMMUNITY_ERROR_TEXT = "There was an error deleting the community. Please try again.";
        private const string LEAVE_COMMUNITY_ERROR_TEXT = "There was an error leaving the community. Please try again.";
        private const int WARNING_NOTIFICATION_DURATION_MS = 3000;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IMVCManager mvcManager;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly IEventsApiService eventsApiService;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly GalleryEventBus galleryEventBus;

        private CameraReelGalleryController? cameraReelGalleryController;
        private MembersListController? membersListController;
        private PlacesSectionController? placesSectionController;
        private EventListController? eventListController;
        private CancellationTokenSource sectionCancellationTokenSource = new ();
        private CancellationTokenSource panelCancellationTokenSource = new ();
        private CancellationTokenSource communityOperationsCancellationTokenSource = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();
        private ISpriteCache? spriteCache;
        private bool isSpriteCacheExternal;
        private readonly ThumbnailLoader thumbnailLoader;

        private GetCommunityResponse.CommunityData communityData;
        private string[] communityPlaceIds;

        public CommunityCardController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ObjectProxy<IFriendsService> friendServiceProxy,
            CommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            ProfileRepositoryWrapper profileDataProvider,
            IPlacesAPIService placesAPIService,
            IRealmNavigator realmNavigator,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            IEventsApiService eventsApiService,
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus,
            IWeb3IdentityCache web3IdentityCache,
            GalleryEventBus galleryEventBus)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.webRequestController = webRequestController;
            this.profileRepositoryWrapper = profileDataProvider;
            this.placesAPIService = placesAPIService;
            this.realmNavigator = realmNavigator;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.eventsApiService = eventsApiService;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.galleryEventBus = galleryEventBus;
            this.thumbnailLoader = new ThumbnailLoader(null);

            chatEventBus.OpenPrivateConversationRequested += CloseCardOnConversationRequested;
            communitiesDataProvider.CommunityUpdated += OnCommunityUpdated;
            communitiesDataProvider.CommunityUserRemoved += OnCommunityUserRemoved;
            communitiesDataProvider.CommunityLeft += OnCommunityLeft;
            communitiesDataProvider.CommunityUserBanned += OnUserBannedFromCommunity;
        }

        public override void Dispose()
        {
            if (viewInstance != null)
            {
                viewInstance.SectionChanged -= OnSectionChanged;
                viewInstance.OpenWizardRequested -= OnOpenCommunityWizard;
                viewInstance.OpenChatRequested -= OnOpenCommunityChatAsync;
                viewInstance.JoinCommunity -= JoinCommunity;
                viewInstance.LeaveCommunityRequested -= LeaveCommunityRequested;
                viewInstance.DeleteCommunityRequested -= OnDeleteCommunityRequested;
                viewInstance.CameraReelGalleryConfigs.PhotosView.OpenWizardButtonClicked -= OnOpenCommunityWizard;
            }

            chatEventBus.OpenPrivateConversationRequested -= CloseCardOnConversationRequested;
            communitiesDataProvider.CommunityUpdated -= OnCommunityUpdated;
            communitiesDataProvider.CommunityUserRemoved -= OnCommunityUserRemoved;
            communitiesDataProvider.CommunityLeft -= OnCommunityLeft;
            communitiesDataProvider.CommunityUserBanned -= OnUserBannedFromCommunity;

            sectionCancellationTokenSource.SafeCancelAndDispose();
            panelCancellationTokenSource.SafeCancelAndDispose();
            communityOperationsCancellationTokenSource.SafeCancelAndDispose();

            if (cameraReelGalleryController != null)
                cameraReelGalleryController.ThumbnailClicked -= OnThumbnailClicked;

            cameraReelGalleryController?.Dispose();
            membersListController?.Dispose();
            placesSectionController?.Dispose();
            eventListController?.Dispose();
        }

        private void OnUserBannedFromCommunity(string communityId, string userAddress) =>
            OnCommunityUserRemoved(communityId);

        private void OnCommunityLeft(string communityId, bool success)
        {
            if (success)
                OnCommunityUserRemoved(communityId);
        }

        private void OnCommunityUserRemoved(string communityId)
        {
            if (communityData.id != communityId) return;

            communityData.DecreaseMembersCount();
            viewInstance?.UpdateMemberCount(communityData);
        }

        private void OnCommunityUpdated(string communityId)
        {
            if (!communityId.Equals(communityData.id)) return;

            ResetSubControllers();
            SetDefaultsAndLoadData();
        }

        private void CloseCardOnConversationRequested(string _) =>
            CloseController();

        private void OnDeleteCommunityRequested()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            DeleteCommunityAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid DeleteCommunityAsync(CancellationToken ct)
            {
                Result<bool> result = await communitiesDataProvider.DeleteCommunityAsync(communityData.id, ct)
                                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await viewInstance!.warningNotificationView.AnimatedShowAsync(DELETE_COMMUNITY_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct)
                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                CloseController();
            }
        }

        private void CloseController() =>
            closeIntentCompletionSource.TrySetResult();

        private async void OnOpenCommunityChatAsync()
        {
            try
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
                chatEventBus.OpenCommunityConversationUsingUserId(communityData.id);
                CloseController();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.SectionChanged += OnSectionChanged;
            viewInstance.OpenWizardRequested += OnOpenCommunityWizard;
            viewInstance.OpenChatRequested += OnOpenCommunityChatAsync;
            viewInstance.JoinCommunity += JoinCommunity;
            viewInstance.LeaveCommunityRequested += LeaveCommunityRequested;
            viewInstance.DeleteCommunityRequested += OnDeleteCommunityRequested;
            viewInstance.CameraReelGalleryConfigs.PhotosView.OpenWizardButtonClicked += OnOpenCommunityWizard;

            viewInstance.SetConfirmationDialogDependencies(profileRepositoryWrapper);

            cameraReelGalleryController = new CameraReelGalleryController(viewInstance.CameraReelGalleryConfigs.PhotosView.GalleryView, cameraReelStorageService, cameraReelScreenshotsStorage,
                new ReelGalleryConfigParams(viewInstance.CameraReelGalleryConfigs.GridLayoutFixedColumnCount, viewInstance.CameraReelGalleryConfigs.ThumbnailHeight,
                    viewInstance.CameraReelGalleryConfigs.ThumbnailWidth, false, false), false);
            cameraReelGalleryController.ThumbnailClicked += OnThumbnailClicked;

            membersListController = new MembersListController(viewInstance.MembersListView,
                profileRepositoryWrapper,
                mvcManager,
                friendServiceProxy,
                communitiesDataProvider,
                viewInstance.warningNotificationView,
                sharedSpaceManager,
                chatEventBus,
                web3IdentityCache);

            placesSectionController = new PlacesSectionController(viewInstance.PlacesSectionView,
                thumbnailLoader,
                communitiesDataProvider,
                placesAPIService,
                viewInstance.warningNotificationView,
                viewInstance.successNotificationView,
                realmNavigator,
                mvcManager,
                clipboard,
                webBrowser);

            eventListController = new EventListController(viewInstance.EventListView,
                eventsApiService,
                placesAPIService,
                thumbnailLoader,
                mvcManager,
                viewInstance.warningNotificationView,
                viewInstance.successNotificationView,
                clipboard,
                webBrowser,
                realmNavigator);

            viewInstance.SetCardBackgroundColor(viewInstance.BackgroundColor, BG_SHADER_COLOR_1);
        }

        protected override void OnViewShow() =>
            SetDefaultsAndLoadData();

        private void SetDefaultsAndLoadData()
        {
            panelCancellationTokenSource = panelCancellationTokenSource.SafeRestart();
            closeIntentCompletionSource = new UniTaskCompletionSource();
            viewInstance!.SetDefaults();
            viewInstance.MembersListView.SetSectionButtonsActive(false);
            LoadCommunityDataAsync(panelCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid LoadCommunityDataAsync(CancellationToken ct)
            {
                viewInstance!.SetLoadingState(true);
                //Since it's the tab that is automatically selected when the community card is opened, we set it to loading.
                viewInstance.MembersListView.SetLoadingStateActive(true);

                if (spriteCache == null)
                {
                    isSpriteCacheExternal = inputData.ThumbnailSpriteCache != null;

                    spriteCache = isSpriteCacheExternal ? inputData.ThumbnailSpriteCache! : new SpriteCache(webRequestController);
                    thumbnailLoader.Cache = spriteCache;
                }

                GetCommunityResponse response = await communitiesDataProvider.GetCommunityAsync(inputData.CommunityId, ct);
                communityPlaceIds = (await communitiesDataProvider.GetCommunityPlacesAsync(inputData.CommunityId, ct)).ToArray();
                communityData = response.data;

                viewInstance.SetLoadingState(false);

                viewInstance.SetPanelCancellationToken(ct);
                viewInstance.ConfigureCommunity(communityData, thumbnailLoader);

                viewInstance.ResetToggle(true);

                eventListController?.ShowEvents(communityData, communityPlaceIds, ct);
            }
        }

        protected override void OnViewClose()
        {
            sectionCancellationTokenSource.SafeCancelAndDispose();
            panelCancellationTokenSource.SafeCancelAndDispose();
            communityOperationsCancellationTokenSource.SafeCancelAndDispose();
            spriteCache = null;

            ResetSubControllers();
            viewInstance!.ResetToggle(false);
        }

        private void ResetSubControllers()
        {
            membersListController?.Reset();
            placesSectionController?.Reset();
            eventListController?.Reset();
        }

        private void OnThumbnailClicked(List<CameraReelResponseCompact> reels, int index, 
            Action<CameraReelResponseCompact> reelDeleteIntention, Action<CameraReelResponseCompact> reelListRefreshIntention) =>
            mvcManager.ShowAsync(PhotoDetailController.IssueCommand(new PhotoDetailParameter(reels, index, 
                true, PhotoDetailParameter.CallerContext.CommunityCard, reelDeleteIntention,
                reelListRefreshIntention, galleryEventBus)));

        private void OnSectionChanged(CommunityCardView.Sections section)
        {
            sectionCancellationTokenSource = sectionCancellationTokenSource.SafeRestart();
            switch (section)
            {
                case CommunityCardView.Sections.PHOTOS:
                    viewInstance!.CameraReelGalleryConfigs.PhotosView.SetAdminEmptyTextActive(communityData.role is CommunityMemberRole.moderator or CommunityMemberRole.owner);
                    cameraReelGalleryController!.ShowPlacesGalleryAsync(communityPlaceIds, sectionCancellationTokenSource.Token).Forget();
                    break;
                case CommunityCardView.Sections.MEMBERS:
                    membersListController!.ShowMembersList(communityData, sectionCancellationTokenSource.Token);
                    break;
                case CommunityCardView.Sections.PLACES:
                    placesSectionController!.ShowPlaces(communityData, communityPlaceIds, sectionCancellationTokenSource.Token);
                    break;
            }
        }

        private void OnOpenCommunityWizard()
        {
            mvcManager.ShowAsync(
                CommunityCreationEditionController.IssueCommand(new CommunityCreationEditionParameter(
                    canCreateCommunities: true,
                    communityId: communityData.id,
                    spriteCache!)));
        }

        private void JoinCommunity()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            JoinCommunityAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid JoinCommunityAsync(CancellationToken ct)
            {
                Result<bool> result = await communitiesDataProvider.JoinCommunityAsync(communityData.id, ct)
                                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await viewInstance!.warningNotificationView.AnimatedShowAsync(JOIN_COMMUNITY_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct)
                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                communityData.IncreaseMembersCount();
                viewInstance!.UpdateMemberCount(communityData);

                //Reset member list and fetch the data again so that we pop inside the member list
                membersListController?.Reset();
                membersListController?.ShowMembersList(communityData, sectionCancellationTokenSource.Token);

                viewInstance.ConfigureInteractionButtons(CommunityMemberRole.member);
            }
        }

        private void LeaveCommunityRequested()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            LeaveCommunityAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid LeaveCommunityAsync(CancellationToken ct)
            {
                Result<bool> result = await communitiesDataProvider.LeaveCommunityAsync(communityData.id, ct)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await viewInstance!.warningNotificationView.AnimatedShowAsync(LEAVE_COMMUNITY_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct)
                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                membersListController?.TryRemoveLocalUser();

                viewInstance!.ConfigureInteractionButtons(CommunityMemberRole.none);
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetClosingTasks(closeIntentCompletionSource.Task, ct));
    }
}
