using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Donations;
using DCL.Donations.UI;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Passport;
using DCL.Profiles;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class DonationsPlugin : IDCLGlobalPlugin<DonationsPluginSettings>
    {
        private static readonly decimal[] DEFAULT_RECOMMENDED_TIP_AMOUNTS = { 166, 333, 500 };

        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDonationsService donationsService;
        private readonly IProfileRepository profileRepository;
        private readonly Entity playerEntity;
        private readonly Arch.Core.World world;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IInputBlock inputBlock;
        private readonly ICompositeWeb3Provider compositeWeb3Provider;

        private DonationsPanelController? donationsPanelController;

        public DonationsPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IDonationsService donationsService,
            IProfileRepository profileRepository,
            Entity playerEntity,
            Arch.Core.World world,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IInputBlock inputBlock,
            ICompositeWeb3Provider compositeWeb3Provider)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.playerEntity = playerEntity;
            this.world = world;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.inputBlock = inputBlock;
            this.compositeWeb3Provider = compositeWeb3Provider;

            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.TIP_RECEIVED, OnTipReceivedNotificationClicked);
        }

        public void Dispose()
        {
            donationsPanelController?.Dispose();
            donationsService.Dispose();
        }

        private void OnTipReceivedNotificationClicked(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not TipReceivedNotification)
                return;

            TipReceivedNotification notification = (TipReceivedNotification)parameters[0];

            mvcManager.ShowAndForget(PassportController.IssueCommand(new PassportParams(notification.Metadata.SenderAddress)));
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(DonationsPluginSettings settings, CancellationToken ct)
        {
            DonationsPanelView donationsPanelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.DonationsPanelPrefab, ct: ct)).GetComponent<DonationsPanelView>();
            ControllerBase<DonationsPanelView, DonationsPanelParameter>.ViewFactoryMethod viewFactoryMethod = DonationsPanelController.Preallocate(donationsPanelViewAsset, null, out DonationsPanelView donationsPanelView);

            bool recommendedAmountParseSuccess = FeatureFlagsConfiguration.Instance.TryGetCsvPayload(FeatureFlagsStrings.RECOMMENDED_DONATION_AMOUNT, "main", out var csv) && csv is { Count: >= 1 };

            donationsPanelController = new DonationsPanelController(
                viewFactoryMethod,
                donationsService,
                profileRepository,
                world,
                playerEntity,
                webBrowser,
                decentralandUrlsSource,
                inputBlock,
                compositeWeb3Provider,
                recommendedAmountParseSuccess ? csv![0].Take(3)
                                                      .Select(s => decimal.Parse(s, CultureInfo.InvariantCulture))
                                                      .ToArray() : DEFAULT_RECOMMENDED_TIP_AMOUNTS);

            mvcManager.RegisterController(donationsPanelController);
        }
    }

    [Serializable]
    public class DonationsPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject DonationsPanelPrefab { get; private set; }
    }
}
