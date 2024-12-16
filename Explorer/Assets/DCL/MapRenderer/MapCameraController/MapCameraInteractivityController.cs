using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.Navmap;
using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly INavmapBus navmapBus;
        private readonly Camera camera;
        private readonly List<IMapLayerController> interactableLayers = new ();

        private IParcelHighlightMarker? marker;
        private GameObject? previouslyRaycastedObject;
        private CancellationTokenSource clickCt = new ();
        private IMapRendererMarker? previouslyClickedMarker = null;
        private CancellationTokenSource longHoverCt = new ();
        private Vector2Int previousParcel = Vector2Int.zero;

        public bool HighlightEnabled { get; private set; }

        public MapCameraInteractivityController(
            Transform cameraParent,
            Camera camera,
            IObjectPool<IParcelHighlightMarker> markersPool,
            ICoordsUtils coordsUtils,
            List<IMapLayerController> interactableLayers,
            INavmapBus navmapBus)
        {
            this.cameraParent = cameraParent;
            this.markersPool = markersPool;
            this.coordsUtils = coordsUtils;
            this.navmapBus = navmapBus;
            this.camera = camera;
            this.interactableLayers.AddRange(interactableLayers);
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

        public GameObject? ProcessMousePosition(Vector2 normalizedCoordinates, Vector2 screenPosition)
        {
            GameObject? hitObject;
            RaycastHit2D raycast = Physics2D.Raycast(GetLocalPosition(normalizedCoordinates), Vector2.zero, 10);

            if (raycast.collider != null)
            {
                hitObject = raycast.collider.gameObject;

                if (raycast.collider.gameObject == previouslyRaycastedObject)
                    return hitObject;

                if (previouslyRaycastedObject != null)
                {
                    foreach (IMapLayerController mapLayerController in interactableLayers)
                        mapLayerController.DeHighlightObject(previouslyRaycastedObject);

                    previouslyRaycastedObject = null;
                }

                previouslyRaycastedObject = raycast.collider.gameObject;

                foreach (IMapLayerController mapLayerController in interactableLayers)
                {
                    if (mapLayerController.HighlightObject(raycast.collider.gameObject, out IMapRendererMarker? mapRenderMarker))
                        return hitObject;
                }
            }
            else
            {
                hitObject = null;

                if (previouslyRaycastedObject != null)
                {
                    foreach (IMapLayerController mapLayerController in interactableLayers)
                        mapLayerController.DeHighlightObject(previouslyRaycastedObject);

                    previouslyRaycastedObject = null;
                }

                TryGetParcel(normalizedCoordinates, out Vector2Int parcel);
            }

            return hitObject;
        }

        public GameObject? ProcessMouseClick(Vector2 normalizedCoordinates, Vector2Int parcel)
        {
            clickCt = clickCt.SafeRestart();

            previouslyClickedMarker?.ToggleSelection(false);
            previouslyClickedMarker = null;

            GameObject? hitObject = null;
            RaycastHit2D raycast = Physics2D.Raycast(GetLocalPosition(normalizedCoordinates), Vector2.zero, 10);
            navmapBus.MoveCameraTo(parcel);

            if (raycast.collider != null)
            {
                if (raycast.collider.gameObject == hitObject)
                    return hitObject;

                hitObject = raycast.collider.gameObject;

                foreach (IMapLayerController mapLayerController in interactableLayers)
                    if (mapLayerController.ClickObject(hitObject, clickCt, out IMapRendererMarker? clickedMarker))
                    {
                        previouslyClickedMarker = clickedMarker;
                        return hitObject;
                    }
            }
            else { hitObject = null; }

            return hitObject;
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

        public void ExitRenderImage()
        {
            RemoveHighlight();
            longHoverCt = longHoverCt.SafeRestart();
        }

        public bool TryGetParcel(Vector2 normalizedCoordinates, out Vector2Int parcel)
        {
            bool parcelExists = coordsUtils.TryGetCoordsWithinInteractableBounds(GetLocalPosition(normalizedCoordinates), out parcel);
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

        public void Dispose() { }

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
