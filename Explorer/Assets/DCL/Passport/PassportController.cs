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
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using DCL.Profiles;
using DCL.Profiles.Self;
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

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

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
        private readonly DCLInput dclInput;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportProfileInfoController passportProfileInfoController;
        private readonly List<IPassportModuleController> overviewPassportModules = new ();
        private readonly List<IPassportModuleController> badgesPassportModules = new ();

        private string currentUserId;
        private CancellationTokenSource? characterPreviewLoadingCts;
        private PassportErrorsController? passportErrorsController;
        private PassportCharacterPreviewController? characterPreviewController;
        private PassportSection? currentSection;
        private bool overviewSectionAlreadyLoaded;
        private bool badgesSectionAlreadyLoaded;

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
            DCLInput dclInput,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            BadgesAPIClient badgesAPIClient
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
            this.dclInput = dclInput;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.badgesAPIClient = badgesAPIClient;
            passportProfileInfoController = new PassportProfileInfoController(selfProfile, world, playerEntity);
        }

        protected override void OnViewInstantiated()
        {
            Assert.IsNotNull(world);
            passportErrorsController = new PassportErrorsController(viewInstance.ErrorNotification);
            characterPreviewController = new PassportCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world, characterPreviewEventBus);
            overviewPassportModules.Add(new UserBasicInfo_PassportModuleController(viewInstance.UserBasicInfoModuleView, chatEntryConfiguration, selfProfile, passportErrorsController));
            overviewPassportModules.Add(new UserDetailedInfo_PassportModuleController(viewInstance.UserDetailedInfoModuleView, mvcManager, selfProfile, viewInstance.AddLinkModal, passportErrorsController, passportProfileInfoController));
            overviewPassportModules.Add(new EquippedItems_PassportModuleController(viewInstance.EquippedItemsModuleView, world, rarityBackgrounds, rarityColors, categoryIcons, thumbnailProvider, webBrowser, decentralandUrlsSource, passportErrorsController));
            overviewPassportModules.Add(new BadgesOverview_PassportModuleController(viewInstance.BadgesOverviewModuleView, badgesAPIClient, passportErrorsController));
            badgesPassportModules.Add(new BadgesDetails_PassportModuleController(viewInstance.BadgesDetailsModuleView, badgesAPIClient, passportErrorsController));

            passportProfileInfoController.PublishError += OnPublishError;
            passportProfileInfoController.OnProfilePublished += OnProfilePublished;

            viewInstance.OverviewSectionButton.Button.onClick.AddListener(OpenOverviewSection);
            viewInstance.BadgesSectionButton.Button.onClick.AddListener(OpenBadgesSection);
        }

        private void OnPublishError()
        {
            passportErrorsController!.Show();
        }

        protected override void OnViewShow()
        {
            currentUserId = inputData.UserId;
            overviewSectionAlreadyLoaded = false;
            badgesSectionAlreadyLoaded = false;
            cursor.Unlock();
            OpenOverviewSection();
            dclInput.Shortcuts.Disable();
            dclInput.Camera.Disable();
            dclInput.Player.Disable();
            viewInstance.ErrorNotification.Hide(true);

            PassportOpened?.Invoke(currentUserId);
        }

        protected override void OnViewClose()
        {
            passportErrorsController!.Hide(true);
            dclInput.Shortcuts.Enable();
            dclInput.Camera.Enable();
            dclInput.Player.Enable();
            characterPreviewController!.OnHide();

            characterPreviewLoadingCts.SafeCancelAndDispose();
            foreach (IPassportModuleController module in overviewPassportModules)
                module.Clear();

            foreach (IPassportModuleController module in badgesPassportModules)
                module.Clear();

            currentSection = null;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.BackgroundButton.OnClickAsync(ct));

        public override void Dispose()
        {
            passportErrorsController?.Hide(true);
            characterPreviewLoadingCts.SafeCancelAndDispose();
            characterPreviewController?.Dispose();

            passportProfileInfoController.OnProfilePublished -= OnProfilePublished;
            passportProfileInfoController.PublishError -= OnPublishError;

            foreach (IPassportModuleController module in overviewPassportModules)
                module.Dispose();

            foreach (IPassportModuleController module in badgesPassportModules)
                module.Dispose();
        }

        private async UniTaskVoid LoadPassportSectionAsync(string userId, PassportSection sectionToLoad, CancellationToken ct)
        {
            try
            {
                switch (sectionToLoad)
                {
                    case PassportSection.OVERVIEW when overviewSectionAlreadyLoaded:
                    case PassportSection.BADGES when badgesSectionAlreadyLoaded:
                        return;
                }

                // Load user profile
                var profile = await profileRepository.GetAsync(userId, 0, ct);

                if (profile == null)
                    return;

                viewInstance.BackgroundImage.material.SetColor(BG_SHADER_COLOR_1, chatEntryConfiguration.GetNameColor(profile.Name));

                if (sectionToLoad == PassportSection.OVERVIEW)
                {
                    // Load avatar preview
                    characterPreviewController!.Initialize(profile.Avatar);
                    characterPreviewController.OnShow();
                }

                // Load passport modules
                SetupPassportModules(profile, sectionToLoad);

                if (sectionToLoad == PassportSection.OVERVIEW)
                    overviewSectionAlreadyLoaded = true;
                else
                    badgesSectionAlreadyLoaded = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to load the profile. Please try again!";
                passportErrorsController!.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void SetupPassportModules(Profile profile, PassportSection passportSection)
        {
            var passportModulesToSetup = passportSection == PassportSection.OVERVIEW ? overviewPassportModules : badgesPassportModules;
            foreach (IPassportModuleController module in passportModulesToSetup)
                module.Setup(profile);
        }

        private void OnProfilePublished(Profile profile) =>
            SetupPassportModules(profile, PassportSection.OVERVIEW);

        private void OpenOverviewSection()
        {
            if (currentSection == PassportSection.OVERVIEW)
                return;

            viewInstance.OverviewSectionButton.SetSelected(true);
            viewInstance.BadgesSectionButton.SetSelected(false);
            viewInstance.OverviewSectionPanel.SetActive(true);
            viewInstance.BadgesSectionPanel.SetActive(false);
            viewInstance.MainScroll.content = viewInstance.OverviewSectionPanel.transform as RectTransform;
            viewInstance.MainScroll.verticalNormalizedPosition = 1;
            viewInstance.CharacterPreviewView.gameObject.SetActive(true);

            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadPassportSectionAsync(currentUserId, PassportSection.OVERVIEW, characterPreviewLoadingCts.Token).Forget();
            currentSection = PassportSection.OVERVIEW;
        }

        private void OpenBadgesSection()
        {
            if (currentSection == PassportSection.BADGES)
                return;

            viewInstance.OverviewSectionButton.SetSelected(false);
            viewInstance.BadgesSectionButton.SetSelected(true);
            viewInstance.OverviewSectionPanel.SetActive(false);
            viewInstance.BadgesSectionPanel.SetActive(true);
            viewInstance.MainScroll.content = viewInstance.BadgesSectionPanel.transform as RectTransform;
            viewInstance.MainScroll.verticalNormalizedPosition = 1;
            viewInstance.CharacterPreviewView.gameObject.SetActive(false);

            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadPassportSectionAsync(currentUserId, PassportSection.BADGES, characterPreviewLoadingCts.Token).Forget();
            currentSection = PassportSection.BADGES;
        }
    }
}
