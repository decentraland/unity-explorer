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
        private const float ARRIVAL_TOLERANCE_SQRD = 50;
        private const float MINIMAP_RADIUS = 130;
        private const float MINIMAP_SQR_DISTANCE_TO_HIDE_PIN = 26000;

        internal delegate IPinMarker PinMarkerBuilder(
            IObjectPool<PinMarkerObject> objectsPool,
            IMapCullingController cullingController);

        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly MapPathRenderer mapPathRenderer;
        private readonly IMapCullingController cullingController;
        private readonly IObjectPool<PinMarkerObject> objectsPool;

        private IPinMarker internalPinMarker;
        private IPinMarker currentDestinationPin;
        private bool destinationSet;
        private Vector2 cachedPlayerMarkerPosition;

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
            internalPinMarker = builder(objectsPool, mapCullingController);
            cullingController.StartTracking(internalPinMarker, this);
            mapPathEventBus.OnSetDestination += OnSetDestination;
            mapPathEventBus.OnRemovedDestination += OnRemovedDestination;
            mapPathEventBus.OnUpdatedPlayerPosition += OnUpdatedPlayerPosition;
            internalPinMarker.OnBecameInvisible();
        }

        private void OnUpdatedPlayerPosition(Vector2 newPosition)
        {
            cachedPlayerMarkerPosition = newPosition;
            mapPathRenderer.UpdateOrigin(cachedPlayerMarkerPosition);
            if (destinationSet)
            {
                if (CheckIfArrivedToDestination(cachedPlayerMarkerPosition, mapPathRenderer.DestinationPoint))
                {
                    mapPathEventBus.ArrivedToDestination();
                }
                else
                {
                    UpdatePositionInMinimapEdge(cachedPlayerMarkerPosition, mapPathRenderer.DestinationPoint);
                }
            }
        }

        private static bool CheckIfArrivedToDestination(Vector2 newPosition, Vector2 destinationPosition)
        {
            var difference = newPosition - destinationPosition;
            return difference.sqrMagnitude <= ARRIVAL_TOLERANCE_SQRD;
        }

        private void UpdatePositionInMinimapEdge(Vector2 origin, Vector2 destination)
        {
            var minimapRadius = MINIMAP_RADIUS;
            Vector2 direction = destination - origin;
            float distanceAB = direction.sqrMagnitude;

            if (distanceAB <= MINIMAP_SQR_DISTANCE_TO_HIDE_PIN)
            {
                mapPathEventBus.HidePinInMinimap();
                return;
            }

            direction.Normalize();
            Vector2 intersectionPoint = (direction * minimapRadius);
            mapPathEventBus.UpdatePinPositionInMinimapEdge(intersectionPoint);
        }

        private void OnRemovedDestination()
        {
            destinationSet = false;
            internalPinMarker.OnBecameInvisible();
            mapPathRenderer.gameObject.SetActive(false);
        }

        private void OnSetDestination(Vector2Int parcel, IPinMarker pinMarker)
        {
            destinationSet = true;
            Vector3 mapPosition = coordsUtils.CoordsToPositionWithOffset(parcel);
            mapPathRenderer.gameObject.SetActive(true);
            mapPathRenderer.SetDestination(mapPosition);
            internalPinMarker.OnBecameInvisible();

            if (pinMarker == null)
            {
                currentDestinationPin = internalPinMarker;
                internalPinMarker.OnBecameVisible();
                internalPinMarker.SetPosition(mapPosition, parcel);
                internalPinMarker.SetAsDestination(true);
            }
            else
            {
                currentDestinationPin = pinMarker;
                internalPinMarker.SetAsDestination(false);
                pinMarker.SetAsDestination(true);
            }

            mapPathEventBus.ShowPinInMinimap(currentDestinationPin);
            UpdatePositionInMinimapEdge(cachedPlayerMarkerPosition, mapPathRenderer.DestinationPoint);
        }

        public void OnMapObjectBecameVisible(IPinMarker obj)
        {
            if (internalPinMarker.IsDestination)
            {
                internalPinMarker.OnBecameVisible();
            }
        }

        public void OnMapObjectCulled(IPinMarker obj)
        {
            internalPinMarker.OnBecameInvisible();
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            if (destinationSet)
            {
                mapPathRenderer.gameObject.SetActive(true);
                internalPinMarker.OnBecameVisible();
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
            internalPinMarker.SetZoom(coordsUtils.ParcelSize, baseZoom, newZoom);
        }

        public void ResetToBaseScale()
        {
            internalPinMarker.ResetScale(coordsUtils.ParcelSize);
        }
    }
}
