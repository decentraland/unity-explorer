using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer
{
    public class MapPathController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        internal delegate IPinMarker PinMarkerBuilder(
            IObjectPool<PinMarkerObject> objectsPool,
            IMapCullingController cullingController);

        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly MapPathRenderer mapPathRenderer;
        private readonly IMapCullingController cullingController;
        private readonly IObjectPool<PinMarkerObject> objectsPool;

        private IPinMarker pathDestinationPin;
        private bool destinationSet;

        internal MapPathController(
            IObjectPool<PinMarkerObject> objectsPool,
            PinMarkerBuilder builder,
            Transform instantiationParent,
            IMapPathEventBus mapPathEventBus,
            MapPathRenderer mapPathRenderer,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController) : base(instantiationParent, coordsUtils, cullingController)
        {
            this.mapPathEventBus = mapPathEventBus;
            this.mapPathRenderer = mapPathRenderer;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.cullingController = cullingController;
        }

        public void Initialize()
        {
            pathDestinationPin = builder(objectsPool, mapCullingController);
            cullingController.StartTracking(pathDestinationPin, this);
            mapPathEventBus.OnSetDestination += OnSetDestination;
            mapPathEventBus.OnRemovedDestination += OnRemovedDestination;
            mapPathEventBus.OnUpdatedPlayerPosition += OnUpdatedPlayerPosition;
            pathDestinationPin.OnBecameInvisible();
        }

        private void OnUpdatedPlayerPosition(Vector2 newPosition)
        {
            if (destinationSet) { mapPathRenderer.UpdateOrigin(newPosition); }
        }

        private void OnRemovedDestination()
        {
            destinationSet = false;
            pathDestinationPin.AnimateOut();
            mapPathRenderer.gameObject.SetActive(false);
        }

        private void OnSetDestination(Vector2Int parcel, IPinMarker pinMarker)
        {
            destinationSet = true;
            Vector3 mapPosition = coordsUtils.CoordsToPositionWithOffset(parcel);
            mapPathRenderer.gameObject.SetActive(true);
            mapPathRenderer.SetDestination(mapPosition);

            if (pinMarker == null)
            {
                pathDestinationPin.SetPosition(mapPosition, parcel);
                pathDestinationPin.SetAsDestination(true);
            }
            else
            {
                pinMarker.SetAsDestination(true);
                pathDestinationPin.AnimateOut();
            }
        }

        public void OnMapObjectBecameVisible(IPinMarker obj)
        {
            if (pathDestinationPin.IsDestination) { mapPathEventBus.HidePinInMinimap(); }
        }

        public void OnMapObjectCulled(IPinMarker obj)
        {
            if (pathDestinationPin.IsDestination) { mapPathEventBus.ShowPinInMinimap(null); }
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            if (destinationSet)
            {
                mapPathRenderer.gameObject.SetActive(true);
                pathDestinationPin.AnimateIn();
            }

            return UniTask.CompletedTask;
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            OnRemovedDestination();
            return UniTask.CompletedTask;
        }

        public void ApplyCameraZoom(float baseZoom, float newZoom)
        {
            pathDestinationPin.SetZoom(coordsUtils.ParcelSize, baseZoom, newZoom);
        }

        public void ResetToBaseScale()
        {
            pathDestinationPin.ResetScale(coordsUtils.ParcelSize);
        }
    }
}
