using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer;
using DCL.MapRenderer.ComponentsFactory;
using DCL.PlacesAPIService;
using System.Threading;

namespace Global.Dynamic
{
    public class MapRendererContainer
    {
        public MapRendererTextureContainer TextureContainer { get; private set; }
        public IMapRenderer MapRenderer { get; private set; }

        public static async UniTask<MapRendererContainer> CreateAsync(
            StaticContainer staticContainer,
            IAssetsProvisioner assetsProvisioner,
            MapRendererSettings settings,
            IPlacesAPIService placesAPIService,
            IMapPathEventBus mapPathEventBus,
            CancellationToken ct)
        {
            var textureContainer = new MapRendererTextureContainer();
            var mapRenderer = new MapRenderer(new MapRendererChunkComponentsFactory(assetsProvisioner, settings, staticContainer.WebRequestsContainer.WebRequestController, textureContainer, placesAPIService, mapPathEventBus));
            await mapRenderer.InitializeAsync(ct);
            return new MapRendererContainer { MapRenderer = mapRenderer, TextureContainer = textureContainer };
        }
    }
}
