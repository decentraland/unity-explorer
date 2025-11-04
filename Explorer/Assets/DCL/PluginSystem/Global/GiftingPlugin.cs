using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack.Gifting.Views;
using MVC;
using System.Threading;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Services;
using DCL.Input;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class GiftingPlugin : IDCLGlobalPlugin<GiftingPlugin.GiftingSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IEventBus eventBus;
        private GiftingController? giftingController;

        public GiftingPlugin(IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository,
            IInputBlock inputBlock,
            IWearablesProvider wearablesProvider,
            IThumbnailProvider thumbnailProvider,
            IEventBus eventBus)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock = inputBlock;
            this.wearablesProvider = wearablesProvider;
            this.thumbnailProvider = thumbnailProvider;
            this.eventBus =  eventBus;
        }

        public void Dispose()
        {
            giftingController?.Dispose();
        }

        public async UniTask InitializeAsync(GiftingSettings settings, CancellationToken ct)
        {
            var giftingViewPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftingPrefab, ct))
                .Value.GetComponent<GiftingView>();

            var (rarityColorMappings, categoryIconsMapping, rarityBackgroundsMapping, rarityInfoPanelBackgroundsMapping) = await UniTask
                .WhenAll( assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityColorMappings, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.CategoryIconsMapping, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityBackgroundsMapping, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            var giftingService = new GiftingService();
            var sendGiftCommand = new SendGiftCommand(giftingService);
            var loadThumbnailCommand = new LoadGiftableItemThumbnailCommand(thumbnailProvider, eventBus);

            var gridFactory = new GiftingGridPresenterFactory(eventBus,
                wearablesProvider,
                loadThumbnailCommand,
                rarityColorMappings,
                categoryIconsMapping,
                rarityBackgroundsMapping);
            
            giftingController = new GiftingController(
                GiftingController.CreateLazily(giftingViewPrefab, null),
                profileRepositoryWrapper,
                profileRepository,
                inputBlock,
                gridFactory,
                sendGiftCommand
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

            [field: SerializeField]
            public BackpackSettings BackpackSettings { get; private set; }
        }
    }
}