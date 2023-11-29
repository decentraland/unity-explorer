using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers
{
    public abstract class MapLayerControllerBase
    {
        internal ICoordsUtils coordsUtils { get; }
        protected CancellationTokenSource ctsDisposing { get; }
        protected Transform instantiationParent { get; }
        internal IMapCullingController mapCullingController { get; }

        internal MapLayerControllerBase(Transform instantiationParent, ICoordsUtils coordsUtils, IMapCullingController cullingController)
        {
            ctsDisposing = new CancellationTokenSource();
            this.coordsUtils = coordsUtils;
            this.instantiationParent = instantiationParent;
            this.mapCullingController = cullingController;
        }

        public void Dispose()
        {
            ctsDisposing.Cancel();
            ctsDisposing.Dispose();
            DisposeImpl();
        }

        protected virtual void DisposeImpl() { }


        protected CancellationTokenSource LinkWithDisposeToken(CancellationToken globalCancellation) =>
            CancellationTokenSource.CreateLinkedTokenSource(globalCancellation, ctsDisposing.Token);
    }
}
