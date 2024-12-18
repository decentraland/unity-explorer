﻿using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.Pins;
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

        private IParcelHighlightMarker? marker;

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

        public bool TryGetParcel(Vector2 normalizedCoordinates, out Vector2Int parcel, out IPinMarker? mark)
        {
            bool parcelExists = coordsUtils.TryGetCoordsWithinInteractableBounds(GetLocalPosition(normalizedCoordinates), out parcel);
            mark = null;
            if (parcelExists) { mark = GetPinMarkerOnParcel(parcel); }
            return parcelExists;
        }

        public IPinMarker? GetPinMarkerOnParcel(Vector2Int parcel)
        {
            if (markerController != null) //This check is only needed for tests -_-
            {
                foreach (IPinMarker mark in markerController.markers.Values)
                    if (mark.ParcelPosition == parcel) { return mark; }
            }
            return null;
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
