using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack.Gifting.Views;
using MVC;
using System.Threading;
using DCL.Backpack.Gifting.Presenters;
using DCL.Input;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class GiftingPlugin : IDCLGlobalPlugin<GiftingPlugin.GiftingSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        private GiftingController? giftingController;

        public GiftingPlugin(IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository, IInputBlock inputBlock)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock = inputBlock;
        }

        public void Dispose()
        {
            giftingController?.Dispose();
        }

        public async UniTask InitializeAsync(GiftingSettings settings, CancellationToken ct)
        {
            var giftingViewPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftingPrefab, ct))
                .Value.GetComponent<GiftingView>();

            giftingController = new GiftingController(
                GiftingController.CreateLazily(giftingViewPrefab, null),
                profileRepositoryWrapper,
                profileRepository,
                inputBlock
            );

            mvcManager.RegisterController(giftingController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class GiftingSettings : IDCLPluginSettings
        {
            [field: Header(nameof(GiftingPlugin) + "." + nameof(GiftingSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject GiftingPrefab;
        }
    }
}