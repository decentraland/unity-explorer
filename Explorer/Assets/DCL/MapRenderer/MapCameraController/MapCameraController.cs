using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.Navmap;
using DG.Tweening;
using System;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapCameraController
{
    internal partial class MapCameraController : IMapCameraControllerInternal
    {
        private const float CAMERA_HEIGHT = 0;
        private const int MAX_TEXTURE_SIZE = 4096;
        private const float EXTRA_MAP_MOVEMENT_PADDING = -0.3f;

        // How much farther than a fit a zoomable camera zooms out over a world's map, so the sea surrounds the world.
        private const float WORLD_SEA_ZOOM_FACTOR = 1.25f;

        public event Action<IMapActivityOwner, IMapCameraControllerInternal>? OnReleasing;
        public event Action<float, float, int>? ZoomChanged;

        public MapLayer EnabledLayers { get; private set; }

        public Camera Camera => mapCameraObject.mapCamera;

        public float Zoom => Mathf.InverseLerp(FarthestZoom(), ClosestZoom(), mapCameraObject.mapCamera.orthographicSize);

        public Vector2 LocalPosition => mapCameraObject.mapCamera.transform.localPosition;

        public Vector2 CoordsPosition => coordsUtils.PositionToCoordsUnclamped(LocalPosition);

        private readonly IMapInteractivityControllerInternal interactivityBehavior;
        private readonly ICoordsUtils coordsUtils;
        private readonly IMapCullingController cullingController;
        private readonly MapCameraObject mapCameraObject;
        private readonly Color defaultBackgroundColor;

        private RenderTexture? renderTexture;

        // Zoom Thresholds in Parcels
        private Vector2Int zoomValues;

        private float normalizedZoom;
        private int zoomLevel;

        private Rect cameraPositionBounds;
        private Sequence? translationSequence;
        private bool rented;

        public MapCameraController(
            IMapInteractivityControllerInternal interactivityBehavior,
            MapCameraObject mapCameraObject,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController
        )
        {
            this.interactivityBehavior = interactivityBehavior;
            this.coordsUtils = coordsUtils;
            this.mapCameraObject = mapCameraObject;
            this.cullingController = cullingController;

            mapCameraObject.transform.localPosition = Vector3.up * CAMERA_HEIGHT;
            mapCameraObject.mapCamera.orthographic = true;
            defaultBackgroundColor = mapCameraObject.mapCamera.backgroundColor;

            coordsUtils.VisibleWorldBoundsChanged += OnVisibleWorldBoundsChanged;
        }

        void IMapCameraControllerInternal.Initialize(Vector2Int textureResolution, Vector2Int zoomValues, MapLayer layers)
        {
            textureResolution = ClampTextureResolution(textureResolution);
            renderTexture = new RenderTexture(textureResolution.x, textureResolution.y, 16, RenderTextureFormat.Default, 0);
            // Bilinear and Trilinear make texture blurry
            renderTexture.filterMode = FilterMode.Point;
            renderTexture.autoGenerateMips = false;
            renderTexture.useMipMap = false;

            this.zoomValues = zoomValues * coordsUtils.ParcelSize;

            EnabledLayers = layers;

            mapCameraObject.mapCamera.targetTexture = renderTexture;

            cullingController.OnCameraAdded(this);
            rented = true;

            interactivityBehavior.Initialize(layers);
        }

        private void OnVisibleWorldBoundsChanged()
        {
            if (!rented)
            {
                CalculateCameraPositionBounds();
                return;
            }

            // A world's farthest zoom depends on its bounds
            SetCameraSize(normalizedZoom, zoomLevel);
            SetLocalPosition(mapCameraObject.transform.localPosition);
        }

        public void ResizeTexture(Vector2Int textureResolution)
        {
            if (!Camera) return;

            if (renderTexture != null && renderTexture.IsCreated())
                renderTexture.Release();

            textureResolution = ClampTextureResolution(textureResolution);
            renderTexture!.width = textureResolution.x;
            renderTexture.height = textureResolution.y;
            renderTexture.Create();

            Camera.ResetAspect();

            // A world's farthest zoom depends on the view's aspect
            if (coordsUtils.BoundsAWorld)
                SetCameraSize(normalizedZoom, zoomLevel);

            SetLocalPosition(mapCameraObject.transform.localPosition);
        }

        private Vector2Int ClampTextureResolution(Vector2Int desiredRes)
        {
            float factor = Mathf.Min(1, MAX_TEXTURE_SIZE / (float) Mathf.Max(desiredRes.x, desiredRes.y));
            return Vector2Int.FloorToInt((Vector2) desiredRes * factor);
        }

        public float GetVerticalSizeInLocalUnits() =>
            mapCameraObject.mapCamera.orthographicSize * 2;

        public RenderTexture GetRenderTexture()
        {
            if (renderTexture == null)
                throw new Exception("Trying to get RenderTexture from a not initialized MapCameraController");

            return renderTexture;
        }

        public IMapInteractivityController GetInteractivityController() =>
            interactivityBehavior;

        public void SetZoom(float value, int zoomLevel)
        {
            SetCameraSize(value, zoomLevel);
            // Clamp local position as boundaries are dependent on zoom
            SetLocalPositionClamped(mapCameraObject.transform.localPosition);
            cullingController.SetCameraDirty(this);
        }

        public void SetPosition(Vector2 coordinates)
        {
            translationSequence?.Kill();
            translationSequence = null;

            Vector3 position = coordsUtils.CoordsToPositionUnclamped(coordinates);
            mapCameraObject.transform.localPosition = ClampLocalPosition(new Vector3(position.x, position.y, CAMERA_HEIGHT));
            cullingController.SetCameraDirty(this);
        }

        public void SetLocalPosition(Vector2 localCameraPosition)
        {
            SetLocalPositionClamped(localCameraPosition);
            cullingController.SetCameraDirty(this);
        }

        private void SetLocalPositionClamped(Vector2 localCameraPosition)
        {
            translationSequence?.Kill();
            translationSequence = null;

            mapCameraObject.transform.localPosition = ClampLocalPosition(localCameraPosition);
        }

        public void CenterOnMap() =>
            SetLocalPosition(coordsUtils.VisibleWorldCenter);

        public void SetBackgroundColor(Color? color) =>
            mapCameraObject.mapCamera.backgroundColor = color ?? defaultBackgroundColor;

        public void SetPositionAndZoom(Vector2 coordinates, float zoom)
        {
            translationSequence?.Kill();
            translationSequence = null;

            SetCameraSize(zoom, 3);

            Vector3 position = coordsUtils.CoordsToPositionUnclamped(coordinates);
            mapCameraObject.transform.localPosition = ClampLocalPosition(new Vector3(position.x, position.y, CAMERA_HEIGHT));
            cullingController.SetCameraDirty(this);
        }

        public void TranslateTo(Vector2 coordinates, float duration, Action? onComplete = null)
        {
            translationSequence = DOTween.Sequence()!;

            Vector3 position = coordsUtils.CoordsToPositionUnclamped(coordinates);
            Vector3 targetPosition = ClampLocalPosition(new Vector3(position.x, position.y, CAMERA_HEIGHT));

            translationSequence.Join(mapCameraObject.transform.DOLocalMove(targetPosition, duration).SetEase(Ease.OutQuart)!)
                               .OnComplete(() =>
                                {
                                    CalculateCameraPositionBounds();
                                    cullingController.SetCameraDirty(this);
                                    onComplete?.Invoke();
                                });
        }

        private void SetCameraSize(float zoomCameraValue, int zoomStepLevel)
        {
            zoomCameraValue = Mathf.Clamp01(zoomCameraValue);
            normalizedZoom = zoomCameraValue;
            zoomLevel = zoomStepLevel;

            mapCameraObject.mapCamera.orthographicSize = Mathf.Lerp(FarthestZoom(), ClosestZoom(), zoomCameraValue);

            interactivityBehavior.ApplyCameraZoom(zoomValues.x, mapCameraObject.mapCamera.orthographicSize);
            ZoomChanged?.Invoke(zoomValues.x, mapCameraObject.mapCamera.orthographicSize, zoomStepLevel);

            CalculateCameraPositionBounds();
        }

        private bool IsZoomable() =>
            zoomValues.x < zoomValues.y;

        // Experiment: a zoomable camera's closest step reaches 2 parcels so the finest satellite level (8) shows.
        // Markers and clusters keep scaling against the configured closest zoom (zoomValues.x).
        private float ClosestZoom() =>
            IsZoomable() ? 2 * coordsUtils.ParcelSize : zoomValues.x;

        /// <summary>
        ///     The configured farthest zoom; a zoomable camera over a world's map stops where the whole world fits the view with sea
        ///     around it.
        /// </summary>
        private float FarthestZoom()
        {
            if (!IsZoomable() || !coordsUtils.BoundsAWorld)
                return zoomValues.y;

            Rect bounds = coordsUtils.VisibleWorldBounds;
            float halfHeightToFit = Mathf.Max(bounds.height, bounds.width / mapCameraObject.mapCamera.aspect) / 2f;

            return Mathf.Clamp(halfHeightToFit * WORLD_SEA_ZOOM_FACTOR, ClosestZoom(), zoomValues.y);
        }

        private Vector3 ClampLocalPosition(Vector3 localPos)
        {
            localPos.x = Mathf.Clamp(localPos.x, cameraPositionBounds.xMin, cameraPositionBounds.xMax);
            localPos.y = Mathf.Clamp(localPos.y, cameraPositionBounds.yMin, cameraPositionBounds.yMax);

            return localPos;
        }

        private void CalculateCameraPositionBounds()
        {
            var worldBounds = coordsUtils.VisibleWorldBounds;
            var worldCenter = coordsUtils.VisibleWorldCenter;

            // A fixed-zoom camera, which follows a target, and any camera over a world's map may centre up to the map's edge,
            // showing the sea past it.
            if (coordsUtils.BoundsAWorld || !IsZoomable())
            {
                cameraPositionBounds = worldBounds;
                return;
            }

            var cameraYSize = mapCameraObject.mapCamera.orthographicSize;
            var cameraXSize = cameraYSize * mapCameraObject.mapCamera.aspect;

            var extraPaddingX = cameraXSize * EXTRA_MAP_MOVEMENT_PADDING;
            var extraPaddingY = cameraYSize * EXTRA_MAP_MOVEMENT_PADDING;

            float xMin = worldBounds.xMin + cameraXSize + extraPaddingX;
            float xMax = worldBounds.xMax - cameraXSize - extraPaddingX;

            float yMin = worldBounds.yMin + cameraYSize + extraPaddingY;
            float yMax = worldBounds.yMax - cameraYSize - extraPaddingY;

            if (worldBounds.xMax - worldBounds.xMin < 2 * cameraXSize)
            {
                xMin = worldCenter.x + extraPaddingX;
                xMax = worldCenter.x - extraPaddingX;
            }

            // If the map's height is smaller than the camera's height, add extra padding
            if (worldBounds.yMax - worldBounds.yMin < 2 * cameraYSize)
            {
                yMin = worldCenter.y + extraPaddingY;
                yMax = worldCenter.y - extraPaddingY;
            }

            cameraPositionBounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public void SuspendRendering()
        {
            mapCameraObject.mapCamera.enabled = false;
        }

        public void ResumeRendering()
        {
            mapCameraObject.mapCamera.enabled = true;

            // What the camera shows was skipped while it was suspended; it resumes in place, so nothing else marks it changed.
            if (rented)
                cullingController.SetCameraDirty(this);
        }

        public void SetActive(bool active)
        {
            mapCameraObject.gameObject.SetActive(active);
        }

        public Rect GetCameraRect()
        {
            var cameraYSize = mapCameraObject.mapCamera.orthographicSize;
            var cameraXSize = cameraYSize * mapCameraObject.mapCamera.aspect;

            var size = new Vector2(cameraXSize * 2f, cameraYSize * 2f);

            return new Rect((Vector2) mapCameraObject.transform.localPosition - new Vector2(cameraXSize, cameraYSize), size);
        }

        public void Release(IMapActivityOwner owner)
        {
            cullingController.OnCameraRemoved(this);
            rented = false;
            if (renderTexture != null)
                renderTexture.Release();
            interactivityBehavior.Release();
            OnReleasing?.Invoke(owner, this);
        }

        public void Dispose()
        {
            coordsUtils.VisibleWorldBoundsChanged -= OnVisibleWorldBoundsChanged;
            translationSequence?.Kill();
            translationSequence = null;

            if (mapCameraObject != null)
                UnityObjectUtils.SafeDestroy(mapCameraObject.gameObject);

            interactivityBehavior.Dispose();

            if (renderTexture != null)
                renderTexture.Release();
            renderTexture = null;
        }
    }
}
