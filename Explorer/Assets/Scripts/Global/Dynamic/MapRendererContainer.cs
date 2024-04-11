using Cysharp.Threading.Tasks;
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
            MapRendererSettings settings,
            IPlacesAPIService placesAPIService,
            CancellationToken ct)
        {
            var textureContainer = new MapRendererTextureContainer();
            var mapRenderer = new MapRenderer(new MapRendererChunkComponentsFactory(staticContainer.AssetsProvisioner, settings, staticContainer.WebRequestsContainer.WebRequestController, textureContainer, placesAPIService));
            await mapRenderer.InitializeAsync(ct);
            return new MapRendererContainer { MapRenderer = mapRenderer, TextureContainer = textureContainer };
        }
    }
}
