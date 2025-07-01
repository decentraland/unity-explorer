using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.BadgesAPIService;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.Requests;
using DCL.Input;
using DCL.Input.Component;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.PhotoDetail;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Multiplayer.Profiles.Poses;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Passport.Modules;
using DCL.Passport.Modules.Badges;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
using Utility.Types;

namespace DCL.Passport
{
    public partial class PassportController : ControllerBase<PassportView, PassportController.Params>
    {
        private enum OpenBadgeSectionOrigin
        {
            BUTTON,
            NOTIFICATION
        }

        private const int MUTUAL_PAGE_SIZE = 3;
        private static readonly int BG_SHADER_COLOR_1 = Shader.PropertyToID("_Color1");
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (25, 0);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;
        private const int CONTEXT_MENU_WIDTH = 250;

        private readonly ICursor cursor;
        private readonly IProfileRepository profileRepository;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly World world;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly IWebRequestController webRequestController;
        private readonly PassportProfileInfoController passportProfileInfoController;
        private readonly List<IPassportModuleController> commonPassportModules = new ();
        private readonly List<IPassportModuleController> overviewPassportModules = new ();
        private readonly List<IPassportModuleController> badgesPassportModules = new ();
        private readonly IInputBlock inputBlock;
        private readonly IRemoteMetadata remoteMetadata;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy;
        private readonly int gridLayoutFixedColumnCount;
        private readonly int thumbnailHeight;
        private readonly int thumbnailWidth;
        private readonly bool enableCameraReel;
        private readonly bool enableFriendshipInteractions;
        private readonly bool includeUserBlocking;
        private readonly bool isNameEditorEnabled;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly string[] getUserPositionBuffer = new string[1];
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IChatEventBus chatEventBus;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private CameraReelGalleryController? cameraReelGalleryController;
        private Profile? ownProfile;
        private Profile? targetProfile;
        private bool isOwnProfile;
        private string? currentUserId;
        private CancellationTokenSource? openPassportFromBadgeNotificationCts;
        private CancellationTokenSource? characterPreviewLoadingCts;
        private CancellationTokenSource? photoLoadingCts;
        private CancellationTokenSource? friendshipStatusCts;
        private CancellationTokenSource? friendshipOperationCts;
        private CancellationTokenSource? fetchMutualFriendsCts;
        private PassportErrorsController? passportErrorsController;
        private PassportCharacterPreviewController? characterPreviewController;
        private PassportSection currentSection;
        private PassportSection alreadyLoadedSections;
        private BadgesDetails_PassportModuleController? badgesDetailsPassportModuleController;
        private GenericContextMenu contextMenu;
        private GenericContextMenuElement contextMenuSeparator;
        private GenericContextMenuElement contextMenuJumpInButton;
        private GenericContextMenuElement contextMenuBlockUserButton;

        private UniTaskCompletionSource? contextMenuCloseTask;
        private UniTaskCompletionSource? passportCloseTask;
        private CancellationTokenSource jumpToFriendLocationCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<string, bool>? PassportOpened;
        public event Action<string, bool, string>? BadgesSectionOpened;
        public event Action<string, bool>? BadgeSelected;
        public event Action<string, Vector2Int>? JumpToFriendClicked;
        public event Action? NameClaimRequested;

        public PassportController(
            ViewFactoryMethod viewFactory,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            CharacterPreviewEventBus characterPreviewEventBus,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            World world,
            Entity playerEntity,
            IThumbnailProvider thumbnailProvider,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            BadgesAPIClient badgesAPIClient,
            IWebRequestController webRequestController,
            IInputBlock inputBlock,
            INotificationsBusController notificationBusController,
            IRemoteMetadata remoteMetadata,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ObjectProxy<FriendsConnectivityStatusTracker> friendOnlineStatusCacheProxy,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            IWeb3IdentityCache web3IdentityCache,
            INftNamesProvider nftNamesProvider,
            int gridLayoutFixedColumnCount,
            int thumbnailHeight,
            int thumbnailWidth,
            bool enableCameraReel,
            bool enableFriendshipInteractions,
            bool includeUserBlocking,
            bool isNameEditorEnabled,
            IChatEventBus chatEventBus,
            ISharedSpaceManager sharedSpaceManager,
            ProfileRepositoryWrapper profileDataProvider) : base(viewFactory)
        {
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.world = world;
            this.thumbnailProvider = thumbnailProvider;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.badgesAPIClient = badgesAPIClient;
            this.webRequestController = webRequestController;
            this.inputBlock = inputBlock;
            this.remoteMetadata = remoteMetadata;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.friendServiceProxy = friendServiceProxy;
            this.friendOnlineStatusCacheProxy = friendOnlineStatusCacheProxy;
            this.onlineUsersProvider = onlineUsersProvider;
            this.realmNavigator = realmNavigator;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepositoryWrapper = profileDataProvider;
            this.nftNamesProvider = nftNamesProvider;
            this.gridLayoutFixedColumnCount = gridLayoutFixedColumnCount;
            this.thumbnailHeight = thumbnailHeight;
            this.thumbnailWidth = thumbnailWidth;
            this.enableCameraReel = enableCameraReel;
            this.enableFriendshipInteractions = enableFriendshipInteractions;
            this.includeUserBlocking = includeUserBlocking;
            this.isNameEditorEnabled = isNameEditorEnabled;
            this.chatEventBus = chatEventBus;
            this.sharedSpaceManager = sharedSpaceManager;

            passportProfileInfoController = new PassportProfileInfoController(selfProfile, world, playerEntity);
            notificationBusController.SubscribeToNotificationTypeReceived(NotificationType.BADGE_GRANTED, OnBadgeNotificationReceived);
            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.BADGE_GRANTED, OnBadgeNotificationClicked);

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings((_, _) => { });
        }

        private void ThumbnailClicked(List<CameraReelResponseCompact> reels, int index, Action<CameraReelResponseCompact> reelDeleteIntention) =>
            mvcManager.ShowAsync(PhotoDetailController.IssueCommand(new PhotoDetailParameter(reels, index, false, reelDeleteIntention)));

        protected override void OnViewInstantiated()
        {
            Assert.IsNotNull(world);

            passportErrorsController = new PassportErrorsController(viewInstance!.ErrorNotification);
            characterPreviewController = new PassportCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world, characterPreviewEventBus);
            var userBasicInfoPassportModuleController = new UserBasicInfo_PassportModuleController(viewInstance.UserBasicInfoModuleView, selfProfile, webBrowser, mvcManager, nftNamesProvider, decentralandUrlsSource, isNameEditorEnabled);
            userBasicInfoPassportModuleController.NameClaimRequested += OnNameClaimRequested;
            commonPassportModules.Add(userBasicInfoPassportModuleController);
            overviewPassportModules.Add(new UserDetailedInfo_PassportModuleController(viewInstance.UserDetailedInfoModuleView, mvcManager, selfProfile, viewInstance.AddLinkModal, passportErrorsController, passportProfileInfoController));
            overviewPassportModules.Add(new EquippedItems_PassportModuleController(viewInstance.EquippedItemsModuleView, world, rarityBackgrounds, rarityColors, categoryIcons, thumbnailProvider, webBrowser, decentralandUrlsSource, passportErrorsController));
            overviewPassportModules.Add(new BadgesOverview_PassportModuleController(viewInstance.BadgesOverviewModuleView, badgesAPIClient, passportErrorsController, webRequestController));

            badgesDetailsPassportModuleController = new BadgesDetails_PassportModuleController(viewInstance.BadgesDetailsModuleView, viewInstance.BadgeInfoModuleView, badgesAPIClient, passportErrorsController, webRequestController, selfProfile);
            cameraReelGalleryController = new CameraReelGalleryController(viewInstance.CameraReelGalleryModuleView, cameraReelStorageService, cameraReelScreenshotsStorage, new ReelGalleryConfigParams(gridLayoutFixedColumnCount, thumbnailHeight, thumbnailWidth, false, false), false);
            cameraReelGalleryController.ThumbnailClicked += ThumbnailClicked;
            badgesPassportModules.Add(badgesDetailsPassportModuleController);

            passportProfileInfoController.PublishError += OnPublishError;
            passportProfileInfoController.OnProfilePublished += OnProfilePublished;
            badgesDetailsPassportModuleController.OnBadgeSelected += OnBadgeSelected;

            viewInstance.OverviewSectionButton.Button.onClick.AddListener(OpenOverviewSection);
            viewInstance.BadgesSectionButton.Button.onClick.AddListener(() => OpenBadgesSection());
            viewInstance.PhotosSectionButton.Button.onClick.AddListener(OpenPhotosSection);
            viewInstance.AcceptFriendButton.onClick.AddListener(AcceptFriendship);
            viewInstance.AddFriendButton.onClick.AddListener(SendFriendRequest);
            viewInstance.CancelFriendButton.onClick.AddListener(CancelFriendRequest);
            viewInstance.RemoveFriendButton.onClick.AddListener(RemoveFriend);
            viewInstance.UnblockFriendButton.onClick.AddListener(UnblockUser);
            viewInstance.ContextMenuButton.onClick.AddListener(ShowContextMenu);
            viewInstance.JumpInButton.onClick.AddListener(OnJumpToFriendButtonClicked);
            viewInstance.ChatButton.onClick.AddListener(OnChatButtonClicked);

            viewInstance.PhotosSectionButton.gameObject.SetActive(enableCameraReel);
            viewInstance.FriendInteractionContainer.SetActive(enableFriendshipInteractions);
            viewInstance.MutualFriends.Root.SetActive(enableFriendshipInteractions);

            contextMenu = new GenericContextMenu(CONTEXT_MENU_WIDTH, CONTEXT_MENU_OFFSET, CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, CONTEXT_MENU_ELEMENTS_SPACING)
                         .AddControl(userProfileContextMenuControlSettings)
                         .AddControl(contextMenuSeparator = new GenericContextMenuElement(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right), false))
                         .AddControl(contextMenuJumpInButton = new GenericContextMenuElement(new ButtonContextMenuControlSettings(viewInstance.JumpInText, viewInstance.JumpInSprite,
                              () => FriendListSectionUtilities.JumpToFriendLocation(inputData.UserId, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator,
                                  parcel => JumpToFriendClicked?.Invoke(inputData.UserId, parcel))), false))
                         .AddControl(contextMenuBlockUserButton = new GenericContextMenuElement(new ButtonContextMenuControlSettings(viewInstance.BlockText, viewInstance.BlockSprite, BlockUserClicked), false));
        }

        private void OnChatButtonClicked()
        {
            OnOpenConversationAsync().Forget();
        }

        private async UniTaskVoid OnOpenConversationAsync()
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
            chatEventBus.OpenPrivateConversationUsingUserId(inputData.UserId);
        }

        private void OnJumpToFriendButtonClicked()
        {
            FriendListSectionUtilities.JumpToFriendLocation(inputData.UserId, jumpToFriendLocationCts, getUserPositionBuffer, onlineUsersProvider, realmNavigator,
                parcel => JumpToFriendClicked?.Invoke(inputData.UserId, parcel));
        }

        private void OnNameClaimRequested() =>
            NameClaimRequested?.Invoke();

        private void ShowContextMenu()
        {
            contextMenuCloseTask = new UniTaskCompletionSource();
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, viewInstance!.ContextMenuButton.transform.position, closeTask: contextMenuCloseTask?.Task))).Forget();
        }

        private void OnPublishError()
        {
            passportErrorsController!.Show();
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.ContextMenuButton.gameObject.SetActive(false);
        }

        protected override void OnViewShow()
        {
            currentUserId = inputData.UserId;
            isOwnProfile = inputData.IsOwnProfile;
            alreadyLoadedSections = PassportSection.NONE;
            cursor.Unlock();
            if (string.IsNullOrEmpty(inputData.BadgeIdSelected))
                OpenOverviewSection();
            else
                OpenBadgesSection(inputData.BadgeIdSelected);

            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
            //We disable the buttons, they will be enabled further down if they meet the requisites
            viewInstance!.JumpInButton.gameObject.SetActive(false);
            viewInstance.ChatButton.gameObject.SetActive(false);

            viewInstance.ErrorNotification.Hide(true);

            if (enableFriendshipInteractions)
            {
                ShowFriendshipInteraction();
                ShowMutualFriends();
            }

            PassportOpened?.Invoke(currentUserId, isOwnProfile);
        }

        protected override void OnViewClose()
        {
            passportErrorsController!.Hide(true);

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);

            characterPreviewController!.OnHide();

            characterPreviewLoadingCts.SafeCancelAndDispose();

            foreach (IPassportModuleController module in commonPassportModules)
                module.Clear();

            foreach (IPassportModuleController module in overviewPassportModules)
                module.Clear();

            foreach (IPassportModuleController module in badgesPassportModules)
                module.Clear();

            currentSection = PassportSection.NONE;
            contextMenuCloseTask?.TrySetResult();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance.BackgroundButton.OnClickAsync(ct),
                viewInstance.JumpInButton.OnClickAsync(ct),
                viewInstance.ChatButton.OnClickAsync(ct));

        public override void Dispose()
        {
            passportErrorsController?.Hide(true);
            openPassportFromBadgeNotificationCts.SafeCancelAndDispose();
            characterPreviewLoadingCts.SafeCancelAndDispose();
            characterPreviewController?.Dispose();
            friendshipStatusCts.SafeCancelAndDispose();
            friendshipOperationCts.SafeCancelAndDispose();
            fetchMutualFriendsCts?.SafeCancelAndDispose();
            photoLoadingCts.SafeCancelAndDispose();
            jumpToFriendLocationCts.SafeCancelAndDispose();

            passportProfileInfoController.OnProfilePublished -= OnProfilePublished;
            passportProfileInfoController.PublishError -= OnPublishError;

            foreach (IPassportModuleController module in commonPassportModules)
                module.Dispose();

            foreach (IPassportModuleController module in overviewPassportModules)
                module.Dispose();

            foreach (IPassportModuleController module in badgesPassportModules)
                module.Dispose();
        }

        private async UniTaskVoid LoadPassportSectionAsync(string userId, PassportSection sectionToLoad, CancellationToken ct, string? badgeIdSelected = null)
        {
            try
            {
                if (EnumUtils.HasFlag(alreadyLoadedSections, sectionToLoad))
                    return;

                // Load user profile
                Profile? profile = await profileRepository.GetAsync(userId, 0, remoteMetadata.GetLambdaDomainOrNull(userId), ct);

                if (profile == null)
                    return;

                UpdateBackgroundColor(profile.UserNameColor);

                if (sectionToLoad == PassportSection.OVERVIEW)
                {
                    // Load avatar preview
                    characterPreviewController!.Initialize(profile.Avatar);
                    characterPreviewController.OnShow();
                }

                // Load passport modules
                SetupPassportModules(profile, sectionToLoad, badgeIdSelected);
                alreadyLoadedSections |= sectionToLoad;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while opening the Passport. Please try again!";
                passportErrorsController!.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void UpdateBackgroundColor(Color profileColor)
        {
            Color.RGBToHSV(profileColor, out float h, out float s, out float v);
            viewInstance?.BackgroundImage.material.SetColor(BG_SHADER_COLOR_1, Color.HSVToRGB(h, s, Mathf.Clamp01(v - 0.3f)));
        }

        private void SetupPassportModules(Profile profile, PassportSection passportSection, string? badgeIdSelected = null)
        {
            foreach (IPassportModuleController module in commonPassportModules)
                module.Setup(profile);

            List<IPassportModuleController> passportModulesToSetup = passportSection == PassportSection.OVERVIEW ? overviewPassportModules : badgesPassportModules;

            foreach (IPassportModuleController module in passportModulesToSetup)
            {
                if (module is BadgesDetails_PassportModuleController badgesDetailsController && !string.IsNullOrEmpty(badgeIdSelected))
                    badgesDetailsController.SetBadgeByDefault(badgeIdSelected);

                module.Setup(profile);
            }
        }

        private void OnProfilePublished(Profile profile) =>
            SetupPassportModules(profile, PassportSection.OVERVIEW);

        private void OpenPhotosSection()
        {
            if (currentSection == PassportSection.PHOTOS)
                return;

            photoLoadingCts = photoLoadingCts.SafeRestart();

            viewInstance!.OpenPhotosSection();

            cameraReelGalleryController!.ShowWalletGalleryAsync(currentUserId!, photoLoadingCts.Token).Forget();

            currentSection = PassportSection.PHOTOS;

            if (!viewInstance.CharacterPreviewView.gameObject.activeSelf)
            {
                viewInstance.CharacterPreviewView.gameObject.SetActive(true);
                characterPreviewController?.OnShow();
            }
        }

        private void OpenOverviewSection()
        {
            if (currentSection == PassportSection.OVERVIEW)
                return;

            viewInstance!.OpenOverviewSection();

            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadPassportSectionAsync(currentUserId!, PassportSection.OVERVIEW, characterPreviewLoadingCts.Token).Forget();
            currentSection = PassportSection.OVERVIEW;
            viewInstance.BadgeInfoModuleView.gameObject.SetActive(false);
            characterPreviewController?.OnShow();
        }

        private void OpenBadgesSection(string? badgeIdSelected = null)
        {
            if (currentSection == PassportSection.BADGES)
                return;

            viewInstance!.OpenBadgesSection();

            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadPassportSectionAsync(currentUserId!, PassportSection.BADGES, characterPreviewLoadingCts.Token, badgeIdSelected).Forget();
            currentSection = PassportSection.BADGES;
            viewInstance.BadgeInfoModuleView.gameObject.SetActive(true);
            characterPreviewController?.OnHide(false);
            bool isOwnPassport = ownProfile?.UserId == currentUserId;
            BadgesSectionOpened?.Invoke(currentUserId!, isOwnPassport, OpenBadgeSectionOrigin.BUTTON.ToString());
        }

        private void OnBadgeNotificationReceived(INotification notification) =>
            BadgesUtils.SetBadgeAsNew(((BadgeGrantedNotification)notification).Metadata.Id);

        private void OnBadgeNotificationClicked(object[] parameters)
        {
            string badgeIdToOpen = string.Empty;

            if (parameters.Length > 0 && parameters[0] is BadgeGrantedNotification badgeNotification)
                badgeIdToOpen = badgeNotification.Metadata.Id;

            openPassportFromBadgeNotificationCts = openPassportFromBadgeNotificationCts.SafeRestart();
            OpenPassportFromBadgeNotificationAsync(badgeIdToOpen, openPassportFromBadgeNotificationCts.Token).Forget();
        }

        private async UniTaskVoid OpenPassportFromBadgeNotificationAsync(string badgeIdToOpen, CancellationToken ct)
        {
            try
            {
                ownProfile ??= await selfProfile.ProfileAsync(ct);

                if (ownProfile != null)
                {
                    BadgesSectionOpened?.Invoke(ownProfile.UserId, true, OpenBadgeSectionOrigin.NOTIFICATION.ToString());
                    mvcManager.ShowAsync(IssueCommand(new Params(ownProfile.UserId, badgeIdToOpen, isOwnProfile: true)), ct).Forget();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while opening the Badges section into the Passport. Please try again!";
                passportErrorsController!.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void OnBadgeSelected(string badgeId)
        {
            bool isOwnPassport = ownProfile?.UserId == currentUserId;
            BadgeSelected?.Invoke(badgeId, isOwnPassport);
        }

        private void ShowFriendshipInteraction()
        {
            DisableAllFriendInteractions();

            if (!friendServiceProxy.Configured) return;

            IFriendsService friendService = friendServiceProxy.Object!;

            friendshipStatusCts = friendshipStatusCts.SafeRestart();
            FetchFriendshipStatusAndShowInteractionAsync(friendshipStatusCts.Token).Forget();

            return;

            async UniTaskVoid FetchFriendshipStatusAndShowInteractionAsync(CancellationToken ct)
            {
                // Fetch our own profile since inputData.IsOwnProfile sometimes is wrong
                Profile? ownProfile = await selfProfile.ProfileAsync(ct);
                // Dont show any interaction for our own user
                if (ownProfile?.UserId == inputData.UserId) return;

                FriendshipStatus friendshipStatus = await friendService.GetFriendshipStatusAsync(inputData.UserId, ct);

                switch (friendshipStatus)
                {
                    case FriendshipStatus.NONE:
                        viewInstance!.AddFriendButton.gameObject.SetActive(true);
                        break;
                    case FriendshipStatus.FRIEND:
                        viewInstance!.RemoveFriendButton.gameObject.SetActive(true);
                        break;
                    case FriendshipStatus.REQUEST_SENT:
                        viewInstance!.CancelFriendButton.gameObject.SetActive(true);
                        break;
                    case FriendshipStatus.REQUEST_RECEIVED:
                        viewInstance!.AcceptFriendButton.gameObject.SetActive(true);
                        break;
                    case FriendshipStatus.BLOCKED:
                        viewInstance!.UnblockFriendButton.gameObject.SetActive(true);
                        break;
                }

                bool friendOnlineStatus = friendOnlineStatusCacheProxy.Object!.GetFriendStatus(inputData.UserId) != OnlineStatus.OFFLINE;
                viewInstance!.JumpInButton.gameObject.SetActive(friendOnlineStatus);
                //TODO FRAN: We need to add here the other reasons why this button could be disabled. For now, only if blocked or blocked by.
                viewInstance.ChatButton.gameObject.SetActive(friendshipStatus != FriendshipStatus.BLOCKED && friendshipStatus != FriendshipStatus.BLOCKED_BY);

                await SetupContextMenuAsync(friendshipStatus, ct);
            }
        }

        private async UniTask SetupContextMenuAsync(FriendshipStatus friendshipStatus, CancellationToken ct)
        {
            targetProfile = await profileRepository.GetAsync(inputData.UserId, ct);

            if (targetProfile == null)
            {
                ReportHub.Log(LogType.Error, new ReportData(ReportCategory.FRIENDS), $"Failed to show context menu button for user {inputData.UserId}. Profile is null.");
                return;
            }

            viewInstance!.ContextMenuButton.gameObject.SetActive(true);

            contextMenuJumpInButton.Enabled = friendOnlineStatusCacheProxy.Object!.GetFriendStatus(inputData.UserId) != OnlineStatus.OFFLINE;
            contextMenuBlockUserButton.Enabled = friendshipStatus != FriendshipStatus.BLOCKED && includeUserBlocking;
            contextMenuSeparator.Enabled = contextMenuJumpInButton.Enabled || contextMenuBlockUserButton.Enabled;

            userProfileContextMenuControlSettings.SetInitialData(targetProfile.ToUserData(), UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED);
        }

        private void BlockUserClicked()
        {
            BlockUserClickedAsync(friendshipStatusCts!.Token).Forget();
            async UniTaskVoid BlockUserClickedAsync(CancellationToken ct)
            {
                await mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(new Web3Address(targetProfile!.UserId), targetProfile.Name, BlockUserPromptParams.UserBlockAction.BLOCK)), ct);

                ShowFriendshipInteraction();
            }
        }

        private void ShowMutualFriends()
        {
            var config = viewInstance!.MutualFriends;
            config.Root.SetActive(false);

            if (inputData.IsOwnProfile || (web3IdentityCache.Identity != null && web3IdentityCache.Identity.Address.Equals(inputData.UserId))) return;
            if (!friendServiceProxy.Configured) return;

            IFriendsService friendService = friendServiceProxy.Object!;

            fetchMutualFriendsCts = fetchMutualFriendsCts.SafeRestart();
            FetchMutualFriendsAsync(fetchMutualFriendsCts.Token).Forget();
            return;

            async UniTaskVoid FetchMutualFriendsAsync(CancellationToken ct)
            {
                foreach (var thumbnail in config.Thumbnails)
                    thumbnail.Root.SetActive(false);

                config.Root.SetActive(false);

                // We only request the first page so we show a couple of mutual thumbnails. This is by design
                Result<PaginatedFriendsResult> promiseResult = await friendService.GetMutualFriendsAsync(
                                                                                       inputData.UserId, 0, MUTUAL_PAGE_SIZE, ct)
                                                                                  .SuppressToResultAsync(ReportCategory.FRIENDS);

                if (!promiseResult.Success)
                    return;

                PaginatedFriendsResult mutualFriendsResult = promiseResult.Value;

                config.Root.SetActive(mutualFriendsResult.Friends.Count > 0);
                config.AmountLabel.text = $"{mutualFriendsResult.TotalAmount} Mutual";

                var mutualConfig = config.Thumbnails;

                for (var i = 0; i < mutualConfig.Length; i++)
                {
                    bool friendExists = i < mutualFriendsResult.Friends.Count;
                    mutualConfig[i].Root.SetActive(friendExists);
                    if (!friendExists) continue;
                    FriendProfile mutualFriend = mutualFriendsResult.Friends[i];
                    mutualConfig[i].Picture.Setup(profileRepositoryWrapper, mutualFriend.UserNameColor, mutualFriend.FacePictureUrl, mutualFriend.Address);
                }
            }
        }

        private void DisableAllFriendInteractions()
        {
            viewInstance!.AcceptFriendButton.gameObject.SetActive(false);
            viewInstance.AddFriendButton.gameObject.SetActive(false);
            viewInstance.CancelFriendButton.gameObject.SetActive(false);
            viewInstance.RemoveFriendButton.gameObject.SetActive(false);
            viewInstance.UnblockFriendButton.gameObject.SetActive(false);
        }

        private void RemoveFriend()
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();
            RemoveFriendThenChangeInteractionStatusAsync(friendshipOperationCts.Token).Forget();
            return;

            async UniTaskVoid RemoveFriendThenChangeInteractionStatusAsync(CancellationToken ct)
            {
                await mvcManager.ShowAsync(UnfriendConfirmationPopupController.IssueCommand(new UnfriendConfirmationPopupController.Params
                {
                    UserId = new Web3Address(inputData.UserId),
                }), ct);

                ShowFriendshipInteraction();
            }
        }

        private void UnblockUser()
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();
            UnblockAndThenChangeInteractionStatusAsync(friendshipOperationCts.Token).Forget();
            return;

            async UniTaskVoid UnblockAndThenChangeInteractionStatusAsync(CancellationToken ct)
            {
                await mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(new Web3Address(targetProfile!.UserId), targetProfile.Name, BlockUserPromptParams.UserBlockAction.UNBLOCK)), ct);

                ShowFriendshipInteraction();
            }
        }

        private void CancelFriendRequest()
        {
            if (!friendServiceProxy.Configured) return;

            IFriendsService friendService = friendServiceProxy.Object!;

            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            CancelFriendRequestThenChangeInteractionStatusAsync(friendshipOperationCts.Token).Forget();
            return;

            async UniTaskVoid CancelFriendRequestThenChangeInteractionStatusAsync(CancellationToken ct)
            {
                await friendService.CancelFriendshipAsync(inputData.UserId, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                ShowFriendshipInteraction();
            }
        }

        private void SendFriendRequest()
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            ShowFriendRequestUIAsync(friendshipOperationCts.Token).Forget();
            return;

            async UniTaskVoid ShowFriendRequestUIAsync(CancellationToken ct)
            {
                await mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                {
                    DestinationUser = new Web3Address(inputData.UserId),
                }), ct);

                ShowFriendshipInteraction();
            }
        }

        private void AcceptFriendship()
        {
            if (!friendServiceProxy.Configured) return;

            IFriendsService friendService = friendServiceProxy.Object!;

            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            AcceptFriendRequestThenChangeInteractionStatusAsync(friendshipOperationCts.Token).Forget();
            return;

            async UniTaskVoid AcceptFriendRequestThenChangeInteractionStatusAsync(CancellationToken ct)
            {
                await friendService.AcceptFriendshipAsync(inputData.UserId, ct);

                ShowFriendshipInteraction();
            }
        }
    }
}
