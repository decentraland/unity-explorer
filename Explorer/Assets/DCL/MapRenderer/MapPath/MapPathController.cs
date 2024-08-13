using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer
{
    public class MapPathController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        internal delegate IPinMarker PinMarkerBuilder(IObjectPool<PinMarkerObject> objectsPool, IMapCullingController cullingController);
        private const float ARRIVAL_TOLERANCE_SQUARED = 50;
        private const float MINIMAP_RADIUS = 134;
        private const float MINIMAP_SQR_DISTANCE_TO_HIDE_PIN = 26000;

        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly MapPathRenderer mapPathRenderer;
        private readonly IMapCullingController cullingController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly IObjectPool<PinMarkerObject> objectsPool;
        private readonly DestinationReachedNotification destinationReachedNotification = new()
            { Type = NotificationType.INTERNAL_ARRIVED_TO_DESTINATION };

        private IPinMarker internalPinMarker;
        private IPinMarker? currentDestinationPin;
        private bool destinationSet;
        private Vector2 cachedPlayerMarkerPosition;

        internal MapPathController(
            IObjectPool<PinMarkerObject> objectsPool,
            PinMarkerBuilder builder,
            Transform instantiationParent,
            IMapPathEventBus mapPathEventBus,
            MapPathRenderer mapPathRenderer,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            INotificationsBusController notificationsBusController) : base(instantiationParent, coordsUtils, cullingController)
        {
            this.mapPathEventBus = mapPathEventBus;
            this.mapPathRenderer = mapPathRenderer;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.cullingController = cullingController;
            this.notificationsBusController = notificationsBusController;
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

            if (destinationSet)
            {
                if (CheckIfArrivedToDestination(cachedPlayerMarkerPosition, mapPathRenderer.DestinationPoint))
                {
                    mapPathEventBus.ArrivedToDestination();
                    notificationsBusController.AddNotification(destinationReachedNotification);
                }
                else
                {
                    mapPathRenderer.UpdateOrigin(cachedPlayerMarkerPosition, true);
                    UpdatePositionInMinimapEdge(cachedPlayerMarkerPosition, mapPathRenderer.DestinationPoint);
                }
            }
        }

        private static bool CheckIfArrivedToDestination(Vector2 newPosition, Vector2 destinationPosition)
        {
            Vector2 difference = newPosition - destinationPosition;
            return difference.sqrMagnitude <= ARRIVAL_TOLERANCE_SQUARED;
        }

        private void UpdatePositionInMinimapEdge(Vector2 origin, Vector2 destination)
        {
            Vector2 direction = destination - origin;
            float distanceAB = direction.sqrMagnitude;

            if (distanceAB <= MINIMAP_SQR_DISTANCE_TO_HIDE_PIN)
            {
                mapPathEventBus.HidePinInMinimap();
                return;
            }

            direction.Normalize();
            Vector2 intersectionPoint = direction * MINIMAP_RADIUS;
            mapPathEventBus.UpdatePinPositionInMinimapEdge(intersectionPoint);
        }

        private void OnRemovedDestination()
        {
            destinationSet = false;
            if (internalPinMarker.IsVisible) { internalPinMarker.Hide(internalPinMarker.OnBecameInvisible); }
            mapPathRenderer.gameObject.SetActive(false);
        }

        private void OnSetDestination(Vector2Int parcel, IPinMarker? pinMarker)
        {
            destinationSet = true;
            Vector3 mapPosition = coordsUtils.CoordsToPositionWithOffset(parcel);
            mapPathRenderer.gameObject.SetActive(true);
            mapPathRenderer.UpdateOrigin(cachedPlayerMarkerPosition);
            mapPathRenderer.SetDestination(mapPosition);
            if (internalPinMarker.IsVisible) { internalPinMarker.Hide(internalPinMarker.OnBecameInvisible); }

            if (pinMarker == null)
            {
                currentDestinationPin = internalPinMarker;
                internalPinMarker.SetAsDestination(true);
                internalPinMarker.OnBecameVisible();
                internalPinMarker.SetPosition(mapPosition, parcel);
                internalPinMarker.Show(null);
            }
            else
            {
                if (currentDestinationPin != null) { currentDestinationPin.SetAsDestination(false); }
                currentDestinationPin = pinMarker;
                pinMarker.SetAsDestination(true);
            }

            mapPathEventBus.ShowPinInMinimap(currentDestinationPin);
            UpdatePositionInMinimapEdge(cachedPlayerMarkerPosition, mapPathRenderer.DestinationPoint);
        }

        public void OnMapObjectBecameVisible(IPinMarker obj)
        {
            if (internalPinMarker.IsDestination)
            {
                internalPinMarker.OnBecameInvisible();
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
            mapPathRenderer.SetZoom(baseZoom, newZoom);
        }

        public void ResetToBaseScale()
        {
            internalPinMarker.ResetScale();
            mapPathRenderer.ResetScale();
        }
    }
}
