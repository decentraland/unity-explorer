using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Minimap;
using DCL.PlacesAPIService;
using Global.Dynamic;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : IDCLGlobalPlugin<MinimapPlugin.MinimapSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MVCManager mvcManager;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IPlacesAPIService placesAPIService;

        private MinimapController minimapController;

        public MinimapPlugin(IAssetsProvisioner assetsProvisioner, MVCManager mvcManager, MapRendererContainer mapRendererContainer, IPlacesAPIService placesAPIService)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(MinimapSettings settings, CancellationToken ct)
        {
            minimapController = new MinimapController(
                MinimapController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.MinimapPrefab, ct: ct)).Value.GetComponent<MinimapView>(), null), mapRendererContainer.MapRenderer, mvcManager, placesAPIService);

            mvcManager.RegisterController(minimapController);
            mvcManager.ShowAsync(MinimapController.IssueCommand()).Forget();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var system = TrackPlayerPositionSystem.InjectToWorld(ref builder);
            minimapController.SystemBinding.InjectSystem(system);
        }

        public class MinimapSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ExplorePanelPlugin) + "." + nameof(MinimapSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject MinimapPrefab;
        }
    }
}
