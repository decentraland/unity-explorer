using Cysharp.Threading.Tasks;
using DCL.MapRenderer;
using DCL.MapRenderer.ComponentsFactory;
using System.Threading;

namespace Global.Dynamic
{
    public class MapRendererContainer
    {
        public IMapRenderer MapRenderer { get; private set; }

        public static async UniTask<MapRendererContainer> CreateAsync(StaticContainer staticContainer, MapRendererSettings settings, CancellationToken ct)
        {
            var mapRenderer = new MapRenderer(new MapRendererChunkComponentsFactory(staticContainer.AssetsProvisioner, settings, staticContainer.WebRequestsContainer.WebRequestController));
            await mapRenderer.InitializeAsync(ct);
            return new MapRendererContainer { MapRenderer = mapRenderer };
        }
    }
}
