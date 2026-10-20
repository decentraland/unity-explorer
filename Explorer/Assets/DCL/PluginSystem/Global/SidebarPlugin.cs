using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Browser;
using DCL.Audio;
using DCL.Chat;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications;
using DCL.Notifications.NotificationsMenu;
using DCL.Passport;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox;
using DCL.UI.Controls;
using DCL.MarketplaceCredits;
using DCL.Clipboard;
using DCL.Credits;
using DCL.InWorldCamera;
using DCL.UI.MainUI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Sidebar;
using DCL.UI.Sidebar.HelpMenu;
using DCL.UI.Skybox;
using DCL.UserInAppInitializationFlow;
using DCL.VoiceChat.UI;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using MVC;
using Runtime.Wearables;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class SidebarPlugin : IDCLGlobalPlugin<SidebarPlugin.SidebarSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly MainUIView mainUiView;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly ICompositeWeb3Provider web3Authenticator;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IProfileCache profileCache;
        private readonly Arch.Core.World globalWorld;
        private readonly Entity playerEntity;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly SelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly IDecentralandUrlsSource decentralandUrls;
        private readonly IPassportBridge passportBridge;
        private readonly ChatEventBus chatEventBus;
        private readonly SmartWearableCache smartWearableCache;
        private readonly SupportRequestService supportRequestService;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly InWorldCameraToggle cameraToggle;
        private readonly AudioMixer generalAudioMixer;
        private readonly VolumeBus volumeBus;
        private readonly ISystemClipboard systemClipboard;

        private SidebarController? sidebarController;
        private NotificationsPanelController? notificationsPanelController;
        private SidebarProfileButtonPresenter? profileButtonPresenter;
        private ProfileMenuController? profileMenuController;
        private SkyboxMenuController? skyboxMenuController;
        private ControlsPanelController? controlsPanelController;
        private SmartWearablesSideBarTooltipController? smartWearablesSideBarTooltipController;
        private SidebarSettingsWidgetController? sidebarSettingsWidgetController;
        private NearbyVoicePanelController? nearbyVoicePanelController;
        private HelpMenuController? helpMenuController;
        private ICreditsPanelController? shopCreditsPanelController;
        private SidebarProfileMenuPresenter? sidebarProfileMenuPresenter;

        private CancellationTokenSource? controlsShortcutCts;

        public SidebarPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MainUIView mainUiView,
            NotificationsRequestController notificationsRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            UnityAppWebBrowser webBrowser,
            ICompositeWeb3Provider web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            Arch.Core.World globalWorld,
            Entity playerEntity,
            ProfileRepositoryWrapper profileDataProvider,
            ProfileChangesBus profileChangesBus,
            SelfProfile selfProfile,
            IRealmData realmData,
            ISceneRestrictionBusController sceneRestrictionBusController,
            IDecentralandUrlsSource decentralandUrls,
            IPassportBridge passportBridge,
            ChatEventBus chatEventBus,
            SmartWearableCache smartWearableCache,
            SupportRequestService supportRequestService,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            InWorldCameraToggle cameraToggle,
            AudioMixer generalAudioMixer,
            VolumeBus volumeBus,
            ISystemClipboard systemClipboard)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mainUiView = mainUiView;
            this.notificationsRequestController = notificationsRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.profileCache = profileCache;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
            profileRepositoryWrapper = profileDataProvider;
            this.profileChangesBus = profileChangesBus;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.decentralandUrls = decentralandUrls;
            this.passportBridge = passportBridge;
            this.smartWearableCache = smartWearableCache;
            this.chatEventBus = chatEventBus;
            this.supportRequestService = supportRequestService;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.cameraToggle = cameraToggle;
            this.generalAudioMixer = generalAudioMixer;
            this.volumeBus = volumeBus;
            this.systemClipboard = systemClipboard;
        }

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.Controls.performed -= OnControlsShortcutPerformed;
            DCLInput.Instance.Shortcuts.Support.performed -= OnSupportShortcutPerformed;
            controlsShortcutCts.SafeCancelAndDispose();

            sidebarController?.Dispose();
            notificationsPanelController?.Dispose();
            profileButtonPresenter?.Dispose();
            profileMenuController?.Dispose();
            skyboxMenuController?.Dispose();
            controlsPanelController?.Dispose();
            smartWearablesSideBarTooltipController?.Dispose();
            sidebarSettingsWidgetController?.Dispose();
            nearbyVoicePanelController?.Dispose();
            shopCreditsPanelController?.Dispose();
            sidebarProfileMenuPresenter?.Dispose();
            helpMenuController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(SidebarSettings settings, CancellationToken ct)
        {
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct)).Value;
            NotificationDefaultThumbnails notificationDefaultThumbnails = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationDefaultThumbnailsSO, ct: ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);

            // TODO move to contextual load pattern
            ControlsPanelView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ControlsPanelPrefab, ct)).GetComponent<ControlsPanelView>();
            ControlsPanelController.Preallocate(panelViewAsset, null!, out ControlsPanelView controlsPanelView);

            controlsPanelController = new ControlsPanelController(() => controlsPanelView);
            notificationsPanelController = new NotificationsPanelController(() => mainUiView.SidebarView.NotificationsMenuView, notificationsRequestController, notificationIconTypes, notificationDefaultThumbnails, webRequestController, rarityBackgroundMapping, web3IdentityCache, profileRepositoryWrapper, mvcManager);
            profileButtonPresenter = new SidebarProfileButtonPresenter( mainUiView.SidebarView.ProfileWidget, web3IdentityCache, profileRepository, profileChangesBus);
            profileMenuController = new ProfileMenuController(() => mainUiView.SidebarView.ProfileMenuView, web3IdentityCache, globalWorld, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, passportBridge, profileRepositoryWrapper);
            skyboxMenuController = new SkyboxMenuController(() => mainUiView.SidebarView.SkyboxMenuView, settings.SettingsAsset, sceneRestrictionBusController);
            smartWearablesSideBarTooltipController = new SmartWearablesSideBarTooltipController(() => mainUiView.SidebarView.SmartWearablesTooltipView, smartWearableCache);
            sidebarSettingsWidgetController = new SidebarSettingsWidgetController(() => mainUiView.SidebarView.SidebarConfigPanelView);
            nearbyVoicePanelController = new NearbyVoicePanelController(() => mainUiView.SidebarView.NearbyVoiceWidget);
            helpMenuController = new HelpMenuController(() => mainUiView.SidebarView.HelpMenu, mvcManager, webBrowser, supportRequestService);

            shopCreditsPanelController = await CreditsPanelSetup.EnableIfUserAllowedAsync(
                mainUiView.SidebarView.ShopCreditsPanel, marketplaceCreditsAPIClient, profileChangesBus, web3IdentityCache, mvcManager, ct);

            sidebarProfileMenuPresenter = new SidebarProfileMenuPresenter(
                mainUiView.SidebarView, web3IdentityCache, profileRepository, profileChangesBus,
                systemClipboard, decentralandUrls, passportBridge, mvcManager, web3Authenticator,
                userInAppInitializationFlow, profileCache, globalWorld, playerEntity);

            sidebarController = new SidebarController(() =>
                {
                    SidebarView view = mainUiView.SidebarView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                mvcManager,
                profileButtonPresenter,
                webBrowser,
                selfProfile,
                realmData,
                decentralandUrls,
                chatEventBus,
                supportRequestService,
                sidebarProfileMenuPresenter,
                cameraToggle,
                generalAudioMixer,
                volumeBus,
                web3IdentityCache
                );

            mvcManager.RegisterController(controlsPanelController);
            mvcManager.RegisterController(notificationsPanelController);
            mvcManager.RegisterController(profileMenuController);
            mvcManager.RegisterController(skyboxMenuController);
            mvcManager.RegisterController(smartWearablesSideBarTooltipController);
            mvcManager.RegisterController(sidebarSettingsWidgetController);
            mvcManager.RegisterController(nearbyVoicePanelController);
            mvcManager.RegisterController(helpMenuController);
            mvcManager.RegisterController(sidebarController);

            DCLInput.Instance.Shortcuts.Controls.performed += OnControlsShortcutPerformed;
            DCLInput.Instance.Shortcuts.Support.performed += OnSupportShortcutPerformed;
        }

        [Serializable]
        public class SidebarSettings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set; } = null!;
            [field: SerializeField] public AssetReferenceT<NotificationDefaultThumbnails> NotificationDefaultThumbnailsSO { get; private set; } = null!;
            [field: SerializeField] public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; } = null!;
            [field: SerializeField] public SkyboxSettingsAsset SettingsAsset { get; private set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject ControlsPanelPrefab { get; private set; } = null!;
        }

        private void OnControlsShortcutPerformed(InputAction.CallbackContext _)
        {
            if (controlsPanelController == null) return;

            if (Keyboard.current?.shiftKey.isPressed == true) return;

            if (controlsPanelController.State == ControllerState.ViewHidden)
            {
                controlsShortcutCts = controlsShortcutCts.SafeRestart();
                mvcManager.ShowAsync(ControlsPanelController.IssueCommand(), controlsShortcutCts.Token).Forget();
            }
            else
                controlsShortcutCts.SafeCancelAndDispose();
        }

        private void OnSupportShortcutPerformed(InputAction.CallbackContext _) =>
            supportRequestService.OpenSupport();
    }
}
