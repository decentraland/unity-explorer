using DCL.MapRenderer.MapCameraController;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MapRenderer.ConsumerUtils
{
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(RectTransform))]
    public class PixelPerfectMapRendererTextureProvider : MonoBehaviour
    {
        [NonSerialized]
        private RectTransform? cachedRectTransform;
        private RectTransform rectTransform => cachedRectTransform ??= (RectTransform)transform;

        private RawImage? rawImage;
        private RawImage targetImage => rawImage ??= GetComponent<RawImage>();

        private IMapCameraController? cameraController;
        private Camera? hudCamera;

        private Vector2Int lastResolution;

        private static Vector3[] worldCorners = new Vector3[4];

        /// <summary>
        /// Arms pixel-perfect resizing; the controller's render texture must already be sized with <see cref="GetPixelPerfectTextureResolution"/>.
        /// </summary>
        public void Activate(IMapCameraController newCameraController)
        {
            cameraController = newCameraController;
            lastResolution = GetPixelPerfectTextureResolution();
        }

        public void SetHudCamera(Camera newHudCamera)
        {
            hudCamera = newHudCamera;
        }

        public void Deactivate()
        {
            cameraController = null;
        }

        public Vector2Int GetPixelPerfectTextureResolution()
        {
            // assumes CanvasScale Match Height = 1;

            // translate rect to screen space
            rectTransform.GetWorldCorners(worldCorners);

            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(hudCamera, worldCorners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(hudCamera, worldCorners[2]);

            var screenSize = topRight - bottomLeft;
            return new Vector2Int((int) screenSize.x, (int) screenSize.y);
        }

        // Screen-resolution or canvas-scale changes (e.g. windowed -> fullscreen) alter the on-screen
        // pixel size without a rect change, so OnRectTransformDimensionsChange alone is not enough
        private void LateUpdate()
        {
            ApplyPixelPerfectResolution();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyPixelPerfectResolution();
        }

        private void ApplyPixelPerfectResolution()
        {
            if (cameraController == null)
                return;

            Vector2Int resolution = GetPixelPerfectTextureResolution();

            if (resolution == lastResolution)
                return;

            lastResolution = resolution;
            cameraController.ResizeTexture(resolution);

            targetImage.SetAllDirty();
        }
    }
}
