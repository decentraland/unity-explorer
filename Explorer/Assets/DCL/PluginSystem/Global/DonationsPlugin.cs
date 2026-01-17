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
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
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
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly Entity playerEntity;
        private readonly Arch.Core.World world;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IInputBlock inputBlock;

        private DonationsPanelController? donationsPanelController;

        public DonationsPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IDonationsService donationsService,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            Entity playerEntity,
            Arch.Core.World world,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IInputBlock inputBlock)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.playerEntity = playerEntity;
            this.world = world;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.inputBlock = inputBlock;

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

            mvcManager.ShowAndForget(PassportController.IssueCommand(new PassportParams(notification.Metadata.Sender.Address)));
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(DonationsPluginSettings settings, CancellationToken ct)
        {
            DonationsPanelView donationsPanelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.DonationsPanelPrefab, ct: ct)).GetComponent<DonationsPanelView>();
            ControllerBase<DonationsPanelView, DonationsPanelParameter>.ViewFactoryMethod viewFactoryMethod = DonationsPanelController.Preallocate(donationsPanelViewAsset, null, out DonationsPanelView donationsPanelView);

            bool recommendedAmountParseSuccess = FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.RECOMMENDED_DONATION_AMOUNT, "main", out DonationRecommendedAmount temporalTipsJson);

            donationsPanelController = new DonationsPanelController(
                viewFactoryMethod,
                donationsService,
                profileRepository,
                profileRepositoryWrapper,
                world,
                playerEntity,
                webBrowser,
                decentralandUrlsSource,
                inputBlock,
                recommendedAmountParseSuccess ? temporalTipsJson.amount : DEFAULT_RECOMMENDED_TIP_AMOUNTS);

            mvcManager.RegisterController(donationsPanelController);
        }

        [Serializable]
        private struct DonationRecommendedAmount
        {
            public decimal[] amount;
        }
    }

    [Serializable]
    public class DonationsPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject DonationsPanelPrefab { get; private set; }
    }
}
