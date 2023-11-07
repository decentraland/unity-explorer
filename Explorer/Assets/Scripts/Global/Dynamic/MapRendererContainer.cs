using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCLServices.MapRenderer;
using DCLServices.MapRenderer.ComponentsFactory;
using System.Threading;

namespace Global.Dynamic
{
    public class MapRendererContainer
    {
        public IMapRenderer MapRenderer { get; private set; }

        public static async UniTask<MapRendererContainer> Create(IAssetsProvisioner assetsProvisioner, MapRendererSettings settings, CancellationToken ct)
        {
            MapRenderer mapRenderer = new MapRenderer(new MapRendererChunkComponentsFactory(assetsProvisioner, settings));
            await mapRenderer.InitializeAsync(ct);
            return new MapRendererContainer() { MapRenderer = mapRenderer };
        }
    }
}
