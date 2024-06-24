using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Pins
{
    internal class PinMarkerController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        internal delegate IPinMarker PinMarkerBuilder(
            IObjectPool<PinMarkerObject> objectsPool,
            IMapCullingController cullingController);

        private readonly IObjectPool<PinMarkerObject> objectsPool;
        private readonly PinMarkerBuilder builder;

        private readonly Dictionary<Vector2Int, IPinMarker> markers = new ();
        private readonly List<Vector2Int> vectorCoords = new ();
        private Vector2Int decodePointer;

        private bool isEnabled;

        public PinMarkerController(
            IObjectPool<PinMarkerObject> objectsPool,
            PinMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.builder = builder;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (IPinMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(IPinMarker marker)
        {
            marker.OnBecameVisible();
        }

        public void OnMapObjectCulled(IPinMarker marker)
        {
            marker.OnBecameInvisible();
        }

        public void ApplyCameraZoom(float baseZoom, float zoom)
        {
            foreach (IPinMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            // Make markers invisible to release everything to the pool and stop tracking
            foreach (IPinMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            isEnabled = false;

            return UniTask.CompletedTask;
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (IPinMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;

            return UniTask.CompletedTask;
        }
    }
}
