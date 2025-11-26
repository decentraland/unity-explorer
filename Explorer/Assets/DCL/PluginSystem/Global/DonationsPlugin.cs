using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Donations.UI;
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

        private DonationsPanelController? donationsPanelController;

        public DonationsPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IEthereumApi ethereumApi,
            IScenesCache scenesCache,
            IProfileRepository profileRepository)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.ethereumApi = ethereumApi;
            this.scenesCache = scenesCache;
            this.profileRepository = profileRepository;
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

            donationsPanelController = new DonationsPanelController(
                viewFactoryMethod,
                ethereumApi,
                scenesCache,
                profileRepository);

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
