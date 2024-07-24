using Cysharp.Threading.Tasks;
using DCL.MapRenderer.ComponentsFactory;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Pins;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer
{
    public interface IMapPathEventBus
    {
        public event Action<Vector2Int, bool> OnSetDestination;
        public event Action OnRemovedDestination;

        void SetDestination(Vector2Int parcel, bool toMapPin);

        void RemoveDestination();
    }

    public class MapPathEventBus : IMapPathEventBus
    {
        public event Action<Vector2Int, bool> OnSetDestination;
        public event Action OnRemovedDestination;

        public void SetDestination(Vector2Int parcel, bool toMapPin)
        {
            OnSetDestination?.Invoke(parcel, toMapPin);
        }

        public void RemoveDestination()
        {
            OnRemovedDestination?.Invoke();
        }
    }

    public class PathRendererController : MapLayerControllerBase, IMapCullingListener<IPinMarker>, IMapLayerController, IZoomScalingLayer
    {
        internal delegate PinMarkerObject PinMarkerBuilder(Transform parent);

        private readonly PinMarkerBuilder builder;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly PathRenderer pathRenderer;
        private readonly IMapCullingController cullingController;

        private PathDestinationPin pathDestinationPin;
        private bool destinationSet;

        internal PathRendererController(
            PinMarkerBuilder builder,
            Transform instantiationParent,
            IMapPathEventBus mapPathEventBus,
            PathRenderer pathRenderer,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController) : base(instantiationParent, coordsUtils, cullingController)
        {
            this.mapPathEventBus = mapPathEventBus;
            this.pathRenderer = pathRenderer;
            this.builder = builder;
            this.cullingController = cullingController;
        }

        public void Initialize(Transform originTransform)
        {
            pathDestinationPin = new PathDestinationPin(cullingController, builder(instantiationParent));

            mapPathEventBus.OnSetDestination += OnSetDestination;
            mapPathEventBus.OnRemovedDestination += OnRemovedDestination;
            pathRenderer.SetOrigin(originTransform);
            pathDestinationPin.OnBecameInvisible();
        }

        private void OnRemovedDestination()
        {
            destinationSet = false;
            pathDestinationPin.AnimateOut();
            pathRenderer.gameObject.SetActive(false);
        }

        private void OnSetDestination(Vector2Int parcel, bool toMapPin)
        {
            destinationSet = true;
            Vector3 mapPosition = coordsUtils.CoordsToPositionWithOffset(parcel);
            pathRenderer.gameObject.SetActive(true);
            pathRenderer.SetDestination(mapPosition);

            if (!toMapPin)
            {
                pathDestinationPin.SetPosition(mapPosition, parcel);
                pathDestinationPin.AnimateIn();

                // Activate beating Animation in Pin
                // Mark as force view so its not hidden
            }
        }

        public void OnMapObjectBecameVisible(IPinMarker obj)
        {
            //Remove from minimapBorder
        }

        public void OnMapObjectCulled(IPinMarker obj)
        {
            //Add to minimapBorder
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            if (destinationSet)
            {
                pathRenderer.gameObject.SetActive(true);
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
