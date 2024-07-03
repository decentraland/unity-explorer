using Arch.Core;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.Pins;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.MapCameraController
{
    internal class MapCameraInteractivityController : IMapInteractivityControllerInternal
    {
        private readonly Transform cameraParent;
        private readonly IObjectPool<IParcelHighlightMarker> markersPool;
        private readonly ICoordsUtils coordsUtils;
        private readonly PinMarkerController markerController;
        private readonly Camera camera;

        private IParcelHighlightMarker marker;

        public bool HighlightEnabled { get; private set; }

        public MapCameraInteractivityController(
            Transform cameraParent,
            Camera camera,
            IObjectPool<IParcelHighlightMarker> markersPool,
            ICoordsUtils coordsUtils,
            PinMarkerController markerController)
        {
            this.cameraParent = cameraParent;
            this.markersPool = markersPool;
            this.coordsUtils = coordsUtils;
            this.markerController = markerController;
            this.camera = camera;
        }

        public void HighlightParcel(Vector2Int parcel)
        {
            if (marker == null)
                return;

            // make position discrete
            var localPosition = coordsUtils.CoordsToPosition(parcel, marker);

            marker.Activate();
            marker.SetCoordinates(parcel, localPosition);
        }

        public void Initialize(MapLayer layers)
        {
            HighlightEnabled = EnumUtils.HasFlag(layers, MapLayer.ParcelHoverHighlight);

            if (HighlightEnabled)
                marker = markersPool.Get();
        }

        public void RemoveHighlight()
        {
            marker?.Deactivate();
        }

        //return as out also the eventual data of a map pin, title and desc in a separate struct?
        public bool TryGetParcel(Vector2 normalizedCoordinates, out Vector2Int parcel, out IPinMarker mark)
        {
            mark = null;
            bool parcelExists = coordsUtils.TryGetCoordsWithinInteractableBounds(GetLocalPosition(normalizedCoordinates), out parcel);

            if (parcelExists)
            {
                foreach (IPinMarker pinMarker in markerController.markers.Values)
                {
                    if (pinMarker.ParcelPosition == parcel)
                    {
                        mark = pinMarker;
                        break;
                    }
                }
            }

            return parcelExists;
        }

        public Vector2 GetNormalizedPosition(Vector2Int parcel)
        {
            var discreteLocalPosition = coordsUtils.CoordsToPosition(parcel);

            // Convert local Position to viewPort
            var worldPosition = cameraParent ? cameraParent.TransformPoint(discreteLocalPosition) : discreteLocalPosition;
            var viewPortPosition = camera.WorldToViewportPoint(worldPosition);

            return viewPortPosition;
        }

        private Vector3 GetLocalPosition(Vector2 normalizedCoordinates)
        {
            // normalized position is equal to viewport position
            var worldPoint = camera.ViewportToWorldPoint(normalizedCoordinates);

            var localPosition = cameraParent ? cameraParent.InverseTransformPoint(worldPoint) : worldPoint;
            localPosition.z = 0;
            return localPosition;
        }

        public void Dispose()
        {

        }

        public void Release()
        {
            if (marker != null)
            {
                markersPool.Release(marker);
                marker = null;
            }
        }

        public void ApplyCameraZoom(float baseZoom, float newZoom)
        {
            marker?.SetZoom(baseZoom, newZoom);
        }
    }
}
