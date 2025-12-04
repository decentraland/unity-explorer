using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Donations;
using DCL.Donations.UI;
using DCL.FeatureFlags;
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
        private readonly DonationsService donationsService;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly Entity playerEntity;
        private readonly Arch.Core.World world;
        private readonly IWebBrowser webBrowser;

        private DonationsPanelController? donationsPanelController;

        public DonationsPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            DonationsService donationsService,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            Entity playerEntity,
            Arch.Core.World world,
            IWebBrowser webBrowser)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.playerEntity = playerEntity;
            this.world = world;
            this.webBrowser = webBrowser;
        }

        public void Dispose()
        {
            donationsPanelController?.Dispose();
            donationsService.Dispose();
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
