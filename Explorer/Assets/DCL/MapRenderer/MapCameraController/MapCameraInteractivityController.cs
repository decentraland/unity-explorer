﻿using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.PointsOfInterest;
using DCL.Navmap;
using DCL.UI;
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
        private const float CAMERA_MOVE_SPEED = 1;

        private readonly Transform cameraParent;
        private readonly IObjectPool<IParcelHighlightMarker> markersPool;
        private readonly ICoordsUtils coordsUtils;
        private readonly INavmapBus navmapBus;
        private readonly AudioClipConfig clickAudio;
        private readonly AudioClipConfig hoverAudio;
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
            INavmapBus navmapBus,
            AudioClipConfig clickAudio,
            AudioClipConfig hoverAudio)
        {
            this.cameraParent = cameraParent;
            this.markersPool = markersPool;
            this.coordsUtils = coordsUtils;
            this.navmapBus = navmapBus;
            this.clickAudio = clickAudio;
            this.hoverAudio = hoverAudio;
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

                UIAudioEventsBus.Instance.SendPlayAudioEvent(hoverAudio);
                if (previouslyRaycastedObject != null)
                {
                    foreach (IMapLayerController mapLayerController in interactableLayers)
                        mapLayerController.TryDeHighlightObject(previouslyRaycastedObject);

                    previouslyRaycastedObject = null;
                }

                previouslyRaycastedObject = raycast.collider.gameObject;

                foreach (IMapLayerController mapLayerController in interactableLayers)
                {
                    if (mapLayerController.TryHighlightObject(raycast.collider.gameObject, out IMapRendererMarker? mapRenderMarker))
                        return hitObject;
                }
            }
            else
            {
                hitObject = null;

                if (previouslyRaycastedObject != null)
                {
                    foreach (IMapLayerController mapLayerController in interactableLayers)
                        mapLayerController.TryDeHighlightObject(previouslyRaycastedObject);

                    previouslyRaycastedObject = null;
                }

                TryGetParcel(normalizedCoordinates, out Vector2Int parcel);
            }

            return hitObject;
        }

        public Vector2Int? ProcessMouseClick(Vector2 normalizedCoordinates, Vector2Int parcel)
        {
            clickCt = clickCt.SafeRestart();

            previouslyClickedMarker?.ToggleSelection(false);
            previouslyClickedMarker = null;

            Vector2Int? hitParcel = null;
            RaycastHit2D raycast = Physics2D.Raycast(GetLocalPosition(normalizedCoordinates), Vector2.zero, 10);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(clickAudio);

            if (raycast.collider != null)
            {
                foreach (IMapLayerController mapLayerController in interactableLayers)
                    if (mapLayerController.TryClickObject(raycast.collider.gameObject, clickCt, out IMapRendererMarker? clickedMarker))
                    {
                        previouslyClickedMarker = clickedMarker;
                        hitParcel = clickedMarker?.GetParcelPosition();
                        return hitParcel;
                    }
            }
            else
            {
                navmapBus.MoveCameraTo(parcel, CAMERA_MOVE_SPEED);
                hitParcel = parcel;
            }

            return hitParcel;
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
