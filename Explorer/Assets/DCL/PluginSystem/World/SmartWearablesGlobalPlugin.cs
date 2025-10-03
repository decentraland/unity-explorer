using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.SmartWearables;
using ECS.SceneLifeCycle;
using MVC;
using PortableExperiences.Controller;
using Runtime.Wearables;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.SmartWearables
{
    public class SmartWearablesGlobalPlugin : IDCLGlobalPlugin<SmartWearablesGlobalPlugin.Settings>
    {
        private readonly WearableStorage wearableStorage;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly IScenesCache scenesCache;
        private readonly SmartWearableCache smartWearableCache;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ILoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly IThumbnailProvider thumbnailProvider;

        private SmartWearableAuthorizationPopupController? popupController;

        public SmartWearablesGlobalPlugin(WearableStorage wearableStorage,
            IBackpackEventBus backpackEventBus,
            IPortableExperiencesController portableExperiencesController,
            IScenesCache scenesCache,
            SmartWearableCache smartWearableCache,
            IAssetsProvisioner assetsProvisioner,
            ILoadingStatus loadingStatus,
            IMVCManager mvcManager,
            IThumbnailProvider thumbnailProvider)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
            this.portableExperiencesController = portableExperiencesController;
            this.scenesCache = scenesCache;
            this.smartWearableCache = smartWearableCache;
            this.assetsProvisioner = assetsProvisioner;
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            this.thumbnailProvider = thumbnailProvider;
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            await InitializeAuthorizationPopup(settings, ct);
        }

        private async UniTask InitializeAuthorizationPopup(Settings settings, CancellationToken ct)
        {
            GameObject prefab = await assetsProvisioner.ProvideMainAssetValueAsync(settings.AuthorizationPopup, ct);
            var view = prefab.GetComponent<SmartWearableAuthorizationPopupView>();
            var viewFactory = SmartWearableAuthorizationPopupController.CreateLazily(view, null);

            NftTypeIconSO rarityBackgrounds = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityBackgrounds, ct);
            NFTColorsSO rarityColors = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColors, ct);
            NftTypeIconSO categoryIcons = await assetsProvisioner.ProvideMainAssetValueAsync(settings.CategoryIcons, ct);

            popupController = new SmartWearableAuthorizationPopupController(viewFactory, smartWearableCache, rarityBackgrounds, rarityColors, categoryIcons);
            mvcManager.RegisterController(popupController);
        }

        public void Dispose()
        {
            // noop
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SmartWearableSystem.InjectToWorld(ref builder,
                wearableStorage,
                smartWearableCache,
                backpackEventBus,
                portableExperiencesController,
                scenesCache,
                loadingStatus,
                mvcManager,
                thumbnailProvider);
        }

        public class Settings : IDCLPluginSettings
        {
            public AssetReferenceGameObject AuthorizationPopup;
            public AssetReferenceT<NftTypeIconSO> RarityBackgrounds;
            public AssetReferenceT<NFTColorsSO> RarityColors;
            public AssetReferenceT<NftTypeIconSO> CategoryIcons;
        }
    }
}
