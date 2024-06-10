using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.TeleportPrompt;
using DCL.WebRequests;
using MVC;
using System.Threading;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class TeleportPromptPlugin : IDCLGlobalPlugin<TeleportPromptPlugin.TeleportPromptSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IRealmNavigator realmNavigator;
        private readonly IMVCManager mvcManager;
        private readonly IWebRequestController webRequestController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ICursor cursor;
        private TeleportPromptController teleportPromptController;

        public TeleportPromptPlugin(
            IAssetsProvisioner assetsProvisioner,
            IRealmNavigator realmNavigator,
            IMVCManager mvcManager,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService,
            ICursor cursor)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.realmNavigator = realmNavigator;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            this.placesAPIService = placesAPIService;
            this.cursor = cursor;
        }

        public async UniTask InitializeAsync(TeleportPromptSettings promptSettings, CancellationToken ct)
        {
            teleportPromptController = new TeleportPromptController(
                TeleportPromptController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(promptSettings.TeleportPromptPrefab, ct: ct)).Value.GetComponent<TeleportPromptView>(), null),
                cursor,
                realmNavigator,
                mvcManager,
                webRequestController,
                placesAPIService);

            mvcManager.RegisterController(teleportPromptController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose()
        {
            teleportPromptController.Dispose();
        }

        public class TeleportPromptSettings : IDCLPluginSettings
        {
            [field: Header(nameof(TeleportPromptPlugin) + "." + nameof(TeleportPromptSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject TeleportPromptPrefab;
        }
    }
}
