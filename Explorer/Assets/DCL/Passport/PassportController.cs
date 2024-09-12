using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.BadgesAPIService;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Passport.Modules;
using DCL.Passport.Modules.Badges;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;

namespace DCL.Passport
{
    public partial class PassportController : ControllerBase<PassportView, PassportController.Params>
    {
        private static readonly int BG_SHADER_COLOR_1 = Shader.PropertyToID("_Color1");

        private readonly ICursor cursor;
        private readonly IProfileRepository profileRepository;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
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

        private Profile? ownProfile;
        private string currentUserId;
        private CancellationTokenSource? openPassportFromBadgeNotificationCts;
        private CancellationTokenSource? characterPreviewLoadingCts;
        private PassportErrorsController? passportErrorsController;
        private PassportCharacterPreviewController? characterPreviewController;
        private PassportSection currentSection;
        private PassportSection alreadyLoadedSections;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<string>? PassportOpened;

        public PassportController(
            ViewFactoryMethod viewFactory,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
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
            INotificationsBusController notificationBusController
        ) : base(viewFactory)
        {
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
            this.chatEntryConfiguration = chatEntryConfiguration;
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

            passportProfileInfoController = new PassportProfileInfoController(selfProfile, world, playerEntity);
            notificationBusController.SubscribeToNotificationTypeReceived(NotificationType.BADGE_GRANTED, OnBadgeNotificationReceived);
            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.BADGE_GRANTED, OnBadgeNotificationClicked);
        }

        protected override void OnViewInstantiated()
        {
            Assert.IsNotNull(world);
            passportErrorsController = new PassportErrorsController(viewInstance!.ErrorNotification);
            characterPreviewController = new PassportCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world, characterPreviewEventBus);
            commonPassportModules.Add(new UserBasicInfo_PassportModuleController(viewInstance.UserBasicInfoModuleView, chatEntryConfiguration, selfProfile, passportErrorsController));
            overviewPassportModules.Add(new UserDetailedInfo_PassportModuleController(viewInstance.UserDetailedInfoModuleView, mvcManager, selfProfile, viewInstance.AddLinkModal, passportErrorsController, passportProfileInfoController));
            overviewPassportModules.Add(new EquippedItems_PassportModuleController(viewInstance.EquippedItemsModuleView, world, rarityBackgrounds, rarityColors, categoryIcons, thumbnailProvider, webBrowser, decentralandUrlsSource, passportErrorsController));
            overviewPassportModules.Add(new BadgesOverview_PassportModuleController(viewInstance.BadgesOverviewModuleView, badgesAPIClient, passportErrorsController, webRequestController));
            badgesPassportModules.Add(new BadgesDetails_PassportModuleController(viewInstance.BadgesDetailsModuleView, viewInstance.BadgeInfoModuleView, badgesAPIClient, passportErrorsController, webRequestController, selfProfile));

            passportProfileInfoController.PublishError += OnPublishError;
            passportProfileInfoController.OnProfilePublished += OnProfilePublished;

            viewInstance.OverviewSectionButton.Button.onClick.AddListener(OpenOverviewSection);
            viewInstance.BadgesSectionButton.Button.onClick.AddListener(() => OpenBadgesSection());
        }

        private void OnPublishError()
        {
            passportErrorsController!.Show();
        }

        protected override void OnViewShow()
        {
            currentUserId = inputData.UserId;
            alreadyLoadedSections = PassportSection.NONE;
            cursor.Unlock();

            if (string.IsNullOrEmpty(inputData.BadgeIdSelected))
                OpenOverviewSection();
            else
                OpenBadgesSection(inputData.BadgeIdSelected);

            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.PLAYER);

            viewInstance!.ErrorNotification.Hide(true);
            PassportOpened?.Invoke(currentUserId);
        }

        protected override void OnViewClose()
        {
            passportErrorsController!.Hide(true);

            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.PLAYER);

            characterPreviewController!.OnHide();

            characterPreviewLoadingCts.SafeCancelAndDispose();

            foreach (IPassportModuleController module in commonPassportModules)
                module.Clear();

            foreach (IPassportModuleController module in overviewPassportModules)
                module.Clear();

            foreach (IPassportModuleController module in badgesPassportModules)
                module.Clear();

            currentSection = PassportSection.NONE;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance.BackgroundButton.OnClickAsync(ct));

        public override void Dispose()
        {
            passportErrorsController?.Hide(true);
            openPassportFromBadgeNotificationCts.SafeCancelAndDispose();
            characterPreviewLoadingCts.SafeCancelAndDispose();
            characterPreviewController?.Dispose();

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
                Profile? profile = await profileRepository.GetAsync(userId, 0, ct);

                if (profile == null)
                    return;

                UpdateBackgroundColor(profile.Name);

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

        private void UpdateBackgroundColor(string profileName)
        {
            Color.RGBToHSV(chatEntryConfiguration.GetNameColor(profileName), out float h, out float s, out float v);
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

        private void OpenOverviewSection()
        {
            if (currentSection == PassportSection.OVERVIEW)
                return;

            viewInstance!.OverviewSectionButton.SetSelected(true);
            viewInstance.BadgesSectionButton.SetSelected(false);
            viewInstance.OverviewSectionPanel.SetActive(true);
            viewInstance.BadgesSectionPanel.SetActive(false);
            viewInstance.MainScroll.content = viewInstance.OverviewSectionPanel.transform as RectTransform;
            viewInstance.MainScroll.verticalNormalizedPosition = 1;
            viewInstance.CharacterPreviewView.gameObject.SetActive(true);

            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadPassportSectionAsync(currentUserId, PassportSection.OVERVIEW, characterPreviewLoadingCts.Token).Forget();
            currentSection = PassportSection.OVERVIEW;
            viewInstance.BadgeInfoModuleView.gameObject.SetActive(false);
        }

        private void OpenBadgesSection(string? badgeIdSelected = null)
        {
            if (currentSection == PassportSection.BADGES)
                return;

            viewInstance!.OverviewSectionButton.SetSelected(false);
            viewInstance.BadgesSectionButton.SetSelected(true);
            viewInstance.OverviewSectionPanel.SetActive(false);
            viewInstance.BadgesSectionPanel.SetActive(true);
            viewInstance.MainScroll.content = viewInstance.BadgesSectionPanel.transform as RectTransform;
            viewInstance.MainScroll.verticalNormalizedPosition = 1;
            viewInstance.CharacterPreviewView.gameObject.SetActive(false);

            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadPassportSectionAsync(currentUserId, PassportSection.BADGES, characterPreviewLoadingCts.Token, badgeIdSelected).Forget();
            currentSection = PassportSection.BADGES;
            viewInstance.BadgeInfoModuleView.gameObject.SetActive(true);
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
                    mvcManager.ShowAsync(IssueCommand(new Params(ownProfile.UserId, badgeIdToOpen)), ct).Forget();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while opening the Badges section into the Passport. Please try again!";
                passportErrorsController!.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
