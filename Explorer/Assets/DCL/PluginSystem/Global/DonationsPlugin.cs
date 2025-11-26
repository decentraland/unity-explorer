using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Donations.UI;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.Web3;
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
        private readonly IScenesCache scenesCache;
        private readonly IProfileRepository profileRepository;
        private readonly FeatureFlagsConfiguration featureFlags;

        private DonationsPanelController? donationsPanelController;

        public DonationsPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IEthereumApi ethereumApi,
            IScenesCache scenesCache,
            IProfileRepository profileRepository,
            FeatureFlagsConfiguration featureFlags)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.ethereumApi = ethereumApi;
            this.scenesCache = scenesCache;
            this.profileRepository = profileRepository;
            this.featureFlags = featureFlags;
        }

        public void Dispose()
        {
            donationsPanelController?.Dispose();
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
                scenesCache,
                profileRepository,
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
