using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.SatelliteAtlas
{
#if UNITY_WEBGL
    /// <summary>
    ///     No-op satellite atlas for WebGL: no map image downloads (metamorph API not used).
    /// </summary>
    internal class WebGLSatelliteAtlasStub : MapLayerControllerBase, IMapLayerController
    {
        public WebGLSatelliteAtlasStub(Transform parent, ICoordsUtils coordsUtils, IMapCullingController cullingController)
            : base(parent, coordsUtils, cullingController) { }

        public UniTask InitializeAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask EnableAsync(CancellationToken cancellationToken)
        {
            if (instantiationParent != null)
                instantiationParent.gameObject.SetActive(true);
            return UniTask.CompletedTask;
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            if (instantiationParent != null)
                instantiationParent.gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }
    }
#endif
}
