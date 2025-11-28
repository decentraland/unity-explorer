using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Donations;
using DCL.Donations.UI;
using DCL.FeatureFlags;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class DonationsPlugin : IDCLGlobalPlugin<DonationsPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IEthereumApi ethereumApi;
        private readonly DonationsService donationsService;
        private readonly IProfileRepository profileRepository;
        private readonly FeatureFlagsConfiguration featureFlags;
        private readonly IWebRequestController webRequestController;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly Entity playerEntity;
        private readonly Arch.Core.World world;
        private readonly IRealmData realmData;
        private readonly IPlacesAPIService placesAPIService;

        private DonationsPanelController? donationsPanelController;

        public DonationsPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IEthereumApi ethereumApi,
            DonationsService donationsService,
            IProfileRepository profileRepository,
            FeatureFlagsConfiguration featureFlags,
            IWebRequestController webRequestController,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            Entity playerEntity,
            Arch.Core.World world,
            IRealmData realmData,
            IPlacesAPIService placesAPIService)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.ethereumApi = ethereumApi;
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.featureFlags = featureFlags;
            this.webRequestController = webRequestController;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.playerEntity = playerEntity;
            this.world = world;
            this.realmData = realmData;
            this.placesAPIService = placesAPIService;
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
            ControllerBase<DonationsPanelView>.ViewFactoryMethod viewFactoryMethod = DonationsPanelController.Preallocate(donationsPanelViewAsset, null, out DonationsPanelView donationsPanelView);

            bool recommendedAmountParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.RECOMMENDED_DONATION_AMOUNT, "main", out DonationRecommendedAmount temporalTipsJson);

            donationsPanelController = new DonationsPanelController(
                viewFactoryMethod,
                ethereumApi,
                donationsService,
                profileRepository,
                webRequestController,
                profileRepositoryWrapper,
                world,
                playerEntity,
                realmData,
                placesAPIService,
                recommendedAmountParseSuccess ? temporalTipsJson.amount : 1.0f);

            mvcManager.RegisterController(donationsPanelController);
        }

        [Serializable]
        private struct DonationRecommendedAmount
        {
            public float amount;
        }
    }

    [Serializable]
    public class DonationsPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject DonationsPanelPrefab { get; private set; }
    }
}
