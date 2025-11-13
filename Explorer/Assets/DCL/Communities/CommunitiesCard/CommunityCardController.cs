using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Clipboard;
using DCL.Communities.CommunitiesCard.Announcements;
using DCL.Communities.CommunitiesCard.Events;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunityCreation;
using DCL.Communities.CommunitiesCard.Places;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Friends;
using DCL.Input;
using DCL.Input.Component;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.PhotoDetail;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.VoiceChat;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardController : ControllerBase<CommunityCardView, CommunityCardParameter>
    {
        private static readonly int BG_SHADER_COLOR_1 = Shader.PropertyToID("_Color1");

        private const string GET_COMMUNITY_ERROR_TEXT = "There was an error getting community data. Please try again.";
        private const string GET_USER_INVITES_OR_REQUESTS_ERROR_TEXT = "There was an error getting user invites and requests. Please try again.";
        private const string JOIN_COMMUNITY_ERROR_TEXT = "There was an error joining the community. Please try again.";
        private const string DELETE_COMMUNITY_ERROR_TEXT = "There was an error deleting the community. Please try again.";
        private const string LEAVE_COMMUNITY_ERROR_TEXT = "There was an error leaving the community. Please try again.";
        private const string REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error requesting to join community. Please try again.";
        private const string CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error cancelling join request. Please try again.";
        private const string ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error accepting community invitation. Please try again.";
        private const string REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE = "There was an error rejecting community invitation. Please try again.";

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IMVCManager mvcManager;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly HttpEventsApiService eventsApiService;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly LambdasProfilesProvider lambdasProfilesProvider;
        private readonly GalleryEventBus galleryEventBus;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;

        private CommunityCardVoiceChatPresenter? communityCardVoiceChatController;
        private CameraReelGalleryController? cameraReelGalleryController;
        private MembersListController? membersListController;
        private PlacesSectionController? placesSectionController;
        private EventListController? eventListController;
        private AnnouncementsSectionController? announcementsSectionController;
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
            CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            ProfileRepositoryWrapper profileDataProvider,
            IPlacesAPIService placesAPIService,
            IRealmNavigator realmNavigator,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            HttpEventsApiService eventsApiService,
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache,
            LambdasProfilesProvider lambdasProfilesProvider,
            GalleryEventBus galleryEventBus,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            IProfileRepository profileRepository,
            IInputBlock inputBlock)
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
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.web3IdentityCache = web3IdentityCache;
            this.lambdasProfilesProvider = lambdasProfilesProvider;
            this.galleryEventBus = galleryEventBus;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.profileRepository = profileRepository;
            this.inputBlock = inputBlock;
            this.thumbnailLoader = new ThumbnailLoader(null);

            chatEventBus.OpenPrivateConversationRequested += CloseCardOnConversationRequested;
            communitiesDataProvider.CommunityUpdated += OnCommunityUpdated;
            communitiesDataProvider.CommunityUserRemoved += OnCommunityUserRemoved;
            communitiesDataProvider.CommunityLeft += OnCommunityLeft;
            communitiesDataProvider.CommunityUserBanned += OnUserBannedFromCommunity;

            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.EVENT_CREATED, OnOpenCommunityCardFromNotification);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.COMMUNITY_REQUEST_TO_JOIN_RECEIVED, OnOpenCommunityCardFromNotification);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.COMMUNITY_REQUEST_TO_JOIN_ACCEPTED, OnOpenCommunityCardFromNotification);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_REQUEST_TO_JOIN_ACCEPTED, OnJoinRequestAccepted);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED_CONTENT_VIOLATION, OnCommunityDeleted);
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.COMMUNITY_DELETED, OnCommunityDeleted);
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
                viewInstance.RequestToJoinCommunity -= RequestToJoinCommunity;
                viewInstance.CancelRequestToJoinCommunity -= CancelRequestToJoinCommunity;
                viewInstance.AcceptInvite -= AcceptCommunityInvitation;
                viewInstance.RejectInvite -= RejectCommunityInvitation;
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
            communityCardVoiceChatController?.Dispose();
            announcementsSectionController?.Dispose();
        }

        private void OnOpenCommunityCardFromNotification(object[] parameters)
        {
            if (parameters.Length == 0)
                return;

            string communityId = parameters[0] switch
                                 {
                                     CommunityUserRequestToJoinNotification joinRequestNotification => joinRequestNotification.Metadata.CommunityId,
                                     CommunityUserRequestToJoinAcceptedNotification joinAcceptedNotification => joinAcceptedNotification.Metadata.CommunityId,
                                     CommunityEventCreatedNotification eventCreatedNotification => eventCreatedNotification.Metadata.CommunityId,
                                     _ => string.Empty
                                 };

            if (communityId == string.Empty) return;

            if (State == ControllerState.ViewHidden)
                mvcManager.ShowAndForget(IssueCommand(new CommunityCardParameter(communityId)));
            else
            {
                ResetSubControllers();
                SetDefaultsAndLoadData(communityId);
            }
        }

        private void OnJoinRequestAccepted(INotification notification)
        {
            if (State == ControllerState.ViewHidden || notification is not CommunityUserRequestToJoinAcceptedNotification acceptedNotification)
                return;

            if (communityData.id != acceptedNotification.Metadata.CommunityId)
                return;

            ResetSubControllers();
            SetDefaultsAndLoadData(acceptedNotification.Metadata.CommunityId);
        }

        private void OnCommunityDeleted(INotification notification)
        {
            if (State == ControllerState.ViewHidden)
                return;

            string communityId = notification switch
                                 {
                                     CommunityDeletedNotification communityDeletedNotification => communityDeletedNotification.Metadata.CommunityId,
                                     CommunityDeletedContenViolationNotification ownerCommunityDeletedNotification => ownerCommunityDeletedNotification.Metadata.CommunityId,
                                     _ => string.Empty
                                 };

            if (communityId == string.Empty || communityId != communityData.id) return;

            CloseController();
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
            SetDefaultsAndLoadData(communityId);
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
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(DELETE_COMMUNITY_ERROR_TEXT));
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
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
                chatEventBus.OpenCommunityConversationUsingCommunityId(communityData.id);
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
            viewInstance.RequestToJoinCommunity += RequestToJoinCommunity;
            viewInstance.CancelRequestToJoinCommunity += CancelRequestToJoinCommunity;
            viewInstance.AcceptInvite += AcceptCommunityInvitation;
            viewInstance.RejectInvite += RejectCommunityInvitation;
            viewInstance.CameraReelGalleryConfigs.PhotosView.OpenWizardButtonClicked += OnOpenCommunityWizard;

            cameraReelGalleryController = new CameraReelGalleryController(viewInstance.CameraReelGalleryConfigs.PhotosView.GalleryView, cameraReelStorageService, cameraReelScreenshotsStorage,
                new ReelGalleryConfigParams(viewInstance.CameraReelGalleryConfigs.GridLayoutFixedColumnCount, viewInstance.CameraReelGalleryConfigs.ThumbnailHeight,
                    viewInstance.CameraReelGalleryConfigs.ThumbnailWidth, false, false), false, galleryEventBus);
            cameraReelGalleryController.ThumbnailClicked += OnThumbnailClicked;

            communityCardVoiceChatController = new CommunityCardVoiceChatPresenter(viewInstance.communityCardVoiceChatView, voiceChatOrchestrator);
            communityCardVoiceChatController.ClosePanel += OnClosePanel;
            membersListController = new MembersListController(viewInstance.MembersListView,
                profileRepositoryWrapper,
                mvcManager,
                friendServiceProxy,
                communitiesDataProvider,
                sharedSpaceManager,
                chatEventBus,
                web3IdentityCache);

            placesSectionController = new PlacesSectionController(viewInstance.PlacesSectionView,
                thumbnailLoader,
                communitiesDataProvider,
                placesAPIService,
                realmNavigator,
                mvcManager,
                clipboard,
                webBrowser,
                lambdasProfilesProvider);

            eventListController = new EventListController(viewInstance.EventListView,
                eventsApiService,
                placesAPIService,
                thumbnailLoader,
                mvcManager,
                clipboard,
                webBrowser,
                realmNavigator,
                decentralandUrlsSource);

            if (CommunitiesFeatureAccess.Instance.IsAnnouncementsFeatureEnabled())
            {
                announcementsSectionController = new AnnouncementsSectionController(
                    viewInstance.AnnouncementsSectionView,
                    communitiesDataProvider,
                    profileRepositoryWrapper,
                    web3IdentityCache,
                    profileRepository);
            }

            viewInstance.SetCardBackgroundColor(viewInstance.BackgroundColor, BG_SHADER_COLOR_1);
        }

        private void OnClosePanel()
        {
            OnClosePanelAsync().Forget();
            return;

            async UniTaskVoid OnClosePanelAsync()
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
            }
        }

        protected override void OnViewShow()
        {
            DisableShortcutsInput();
            SetDefaultsAndLoadData(inputData.CommunityId);
        }

        private void SetDefaultsAndLoadData(string communityId)
        {
            panelCancellationTokenSource = panelCancellationTokenSource.SafeRestart();
            closeIntentCompletionSource = new UniTaskCompletionSource();
            viewInstance!.SetDefaults();
            viewInstance.MembersListView.SetSectionButtonsActive(false);
            LoadCommunityDataAsync(panelCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid LoadCommunityDataAsync(CancellationToken ct)
            {
                communityCardVoiceChatController?.Reset();

                viewInstance!.SetLoadingState(true);
                //Since it's the tab that is automatically selected when the community card is opened, we set it to loading.
                if (CommunitiesFeatureAccess.Instance.IsAnnouncementsFeatureEnabled())
                    viewInstance.AnnouncementsSectionView.SetLoadingStateActive(true);
                else
                    viewInstance.MembersListView.SetLoadingStateActive(true);


                if (spriteCache == null)
                {
                    isSpriteCacheExternal = inputData.ThumbnailSpriteCache != null;

                    spriteCache = isSpriteCacheExternal ? inputData.ThumbnailSpriteCache! : new SpriteCache(webRequestController);
                    thumbnailLoader.Cache = spriteCache;
                }

                var getCommunityResult = await communitiesDataProvider.GetCommunityAsync(communityId, ct)
                                                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!getCommunityResult.Success)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_COMMUNITY_ERROR_TEXT));
                    return;
                }

                communityData = getCommunityResult.Value.data;

                // Check if we have a pending invite to the community
                bool existsInvitation = await CheckUserInviteOrRequestAsync(InviteRequestAction.invite, ct);

                if (!communityData.IsAccessAllowed())
                {
                    if (!existsInvitation)
                    {
                        // Check if we have a pending request to join the community
                        await CheckUserInviteOrRequestAsync(InviteRequestAction.request_to_join, ct);
                    }
                }
                else
                    communityPlaceIds = (await communitiesDataProvider.GetCommunityPlacesAsync(communityId, ct)).ToArray();

                viewInstance.SetLoadingState(false);

                viewInstance.SetPanelCancellationToken(ct);
                viewInstance.ConfigureCommunity(communityData, thumbnailLoader);

                if (communityData.IsAccessAllowed())
                {
                    viewInstance.ResetToggle(true);
                    eventListController?.ShowEvents(communityData, ct);
                    communityCardVoiceChatController?.SetPanelStatus(
                        communityData.voiceChatStatus.isActive,
                        communityData.role is CommunityMemberRole.moderator or CommunityMemberRole.owner,
                        communityData.id);

                    communityCardVoiceChatController?.SetListenersCount(communityData.voiceChatStatus.participantCount);
                }
            }

            async UniTask<bool> CheckUserInviteOrRequestAsync(InviteRequestAction action, CancellationToken ct)
            {
                var getUserInviteRequestResult = await communitiesDataProvider.GetUserInviteRequestAsync(action, ct)
                                                                              .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return false;

                if (!getUserInviteRequestResult.Success)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_USER_INVITES_OR_REQUESTS_ERROR_TEXT));
                    return false;
                }

                foreach (var request in getUserInviteRequestResult.Value.data.results)
                    if (string.Equals(request.communityId, communityId, StringComparison.CurrentCultureIgnoreCase))
                    {
                        communityData.SetPendingInviteOrRequestId(request.id);
                        communityData.SetPendingAction(action);
                        return true;
                    }

                return false;
            }
        }

        protected override void OnViewClose()
        {
            sectionCancellationTokenSource.SafeCancelAndDispose();
            panelCancellationTokenSource.SafeCancelAndDispose();
            communityOperationsCancellationTokenSource.SafeCancelAndDispose();
            spriteCache = null;

            RestoreInput();
            ResetSubControllers();
            viewInstance!.ResetToggle(false);
        }

        private void ResetSubControllers()
        {
            membersListController?.Reset();
            placesSectionController?.Reset();
            eventListController?.Reset();
            communityCardVoiceChatController?.Reset();
            announcementsSectionController?.Reset();
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
                case CommunityCardView.Sections.ANNOUNCEMENTS:
                    announcementsSectionController!.ShowAnnouncements(communityData, sectionCancellationTokenSource.Token);
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
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(JOIN_COMMUNITY_ERROR_TEXT));
                    return;
                }

                communityData.SetRole(CommunityMemberRole.member);
                communityData.IncreaseMembersCount();
                viewInstance!.UpdateMemberCount(communityData);

                //Reset member list and fetch the data again so that we pop inside the member list
                membersListController?.Reset();
                membersListController?.ShowMembersList(communityData, sectionCancellationTokenSource.Token);

                viewInstance.ConfigureInteractionButtons(communityData);
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
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(LEAVE_COMMUNITY_ERROR_TEXT));
                    return;
                }

                membersListController?.TryRemoveLocalUser();

                communityData.SetRole(CommunityMemberRole.none);
                viewInstance!.ConfigureInteractionButtons(communityData);
                viewInstance!.SetCommunityAccessAsAllowed(communityData.IsAccessAllowed());
            }
        }

        private void RequestToJoinCommunity()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            RequestToJoinCommunityAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid RequestToJoinCommunityAsync(CancellationToken ct)
            {
                var result = await communitiesDataProvider.SendInviteOrRequestToJoinAsync(communityData.id, web3IdentityCache.Identity?.Address, InviteRequestAction.request_to_join, ct)
                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE));
                }

                communityData.SetPendingInviteOrRequestId(result.Value);
                communityData.SetPendingAction(InviteRequestAction.request_to_join);
                viewInstance!.ConfigureInteractionButtons(communityData);
            }
        }

        private void CancelRequestToJoinCommunity()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            CancelRequestToJoinCommunityAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid CancelRequestToJoinCommunityAsync(CancellationToken ct)
            {
                var result = await communitiesDataProvider.ManageInviteRequestToJoinAsync(communityData.id, communityData.pendingInviteOrRequestId, InviteRequestIntention.cancelled, ct)
                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(CANCEL_REQUEST_TO_JOIN_COMMUNITY_ERROR_MESSAGE));
                }

                communityData.SetPendingInviteOrRequestId(null);
                communityData.SetPendingAction(InviteRequestAction.none);
                viewInstance!.ConfigureInteractionButtons(communityData);
            }
        }

        private void AcceptCommunityInvitation()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            AcceptCommunityInvitationAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid AcceptCommunityInvitationAsync(CancellationToken ct)
            {
                var result = await communitiesDataProvider.ManageInviteRequestToJoinAsync(communityData.id, communityData.pendingInviteOrRequestId, InviteRequestIntention.accepted, ct)
                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(ACCEPT_COMMUNITY_INVITATION_ERROR_MESSAGE));
                }

                if (communityData.privacy == CommunityPrivacy.@public)
                {
                    communityData.SetRole(CommunityMemberRole.member);
                    communityData.SetPendingInviteOrRequestId(null);
                    communityData.SetPendingAction(InviteRequestAction.none);
                    viewInstance!.ConfigureInteractionButtons(communityData);
                }
                else
                {
                    ResetSubControllers();
                    SetDefaultsAndLoadData(communityData.id);
                }
            }
        }

        private void RejectCommunityInvitation()
        {
            communityOperationsCancellationTokenSource = communityOperationsCancellationTokenSource.SafeRestart();
            RejectCommunityInvitationAsync(communityOperationsCancellationTokenSource.Token).Forget();
            return;

            async UniTaskVoid RejectCommunityInvitationAsync(CancellationToken ct)
            {
                var result = await communitiesDataProvider.ManageInviteRequestToJoinAsync(communityData.id, communityData.pendingInviteOrRequestId, InviteRequestIntention.rejected, ct)
                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(REJECT_COMMUNITY_INVITATION_ERROR_MESSAGE));
                }

                CloseController();
            }
        }

        private void DisableShortcutsInput() =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void RestoreInput() =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetClosingTasks(closeIntentCompletionSource.Task, ct));
    }
}
