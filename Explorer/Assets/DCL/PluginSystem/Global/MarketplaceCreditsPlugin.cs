using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCreditsAPIService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Profiles.Self;
using DCL.UI.MainUI;
using DCL.UI.SharedSpaceManager;
using DCL.WebRequests;
using ECS;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class MarketplaceCreditsPlugin : IDCLGlobalPlugin<MarketplaceCreditsPluginSettings>
    {
        private readonly MainUIView mainUIView;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly IMVCManager mvcManager;
        private readonly INotificationsBusController notificationBusController;
        private readonly IRealmData realmData;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private MarketplaceCreditsMenuController? marketplaceCreditsMenuController;
        private CreditsUnlockedController? creditsUnlockedController;

        public MarketplaceCreditsPlugin(
            MainUIView mainUIView,
            IAssetsProvisioner assetsProvisioner,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            INotificationsBusController notificationBusController,
            IRealmData realmData,
            ISharedSpaceManager sharedSpaceManager)
        {
            this.mainUIView = mainUIView;
            this.assetsProvisioner = assetsProvisioner;
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            this.notificationBusController = notificationBusController;
            this.realmData = realmData;
            this.sharedSpaceManager = sharedSpaceManager;

            marketplaceCreditsAPIClient = new MarketplaceCreditsAPIClient(webRequestController, decentralandUrlsSource);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(MarketplaceCreditsPluginSettings settings, CancellationToken ct)
        {
            CreditsUnlockedView creditsUnlockedPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.CreditsUnlockedPrefab, ct))
                                                       .Value.GetComponent<CreditsUnlockedView>();

            creditsUnlockedController = new CreditsUnlockedController(CreditsUnlockedController.CreateLazily(creditsUnlockedPrefab, null));
            mvcManager.RegisterController(creditsUnlockedController);

            marketplaceCreditsMenuController = new MarketplaceCreditsMenuController(() =>
                {
                    var panelView = mainUIView.MarketplaceCreditsMenuView;
                    panelView.gameObject.SetActive(false);
                    return panelView;
                },
                mainUIView.SidebarView.marketplaceCreditsButton,
                webBrowser,
                inputBlock,
                marketplaceCreditsAPIClient,
                selfProfile,
                webRequestController,
                mvcManager,
                notificationBusController,
                mainUIView.SidebarView.marketplaceCreditsButtonAnimator,
                mainUIView.SidebarView.marketplaceCreditsButtonAlertMark,
                realmData,
                sharedSpaceManager);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.MarketplaceCredits, marketplaceCreditsMenuController);
            mvcManager.RegisterController(marketplaceCreditsMenuController);
        }

        public void Dispose() =>
            marketplaceCreditsMenuController?.Dispose();
    }

    public class MarketplaceCreditsPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public AssetReferenceGameObject CreditsUnlockedPrefab { get; set; } = null!;
    }
}
