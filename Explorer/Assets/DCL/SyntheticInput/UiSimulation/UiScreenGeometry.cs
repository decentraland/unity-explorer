using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Conversions between the three coordinate spaces of the UI sublayer:
    ///     - Unity screen pixels: bottom-left origin, used by uGUI raycasts and input devices
    ///     - image pixels: top-left origin, what a driver reads off a screenshot
    ///     - UI Toolkit panel units
    /// </summary>
    public static class UiScreenGeometry
    {
        // Main-thread only: filled and read within one call.
        private static readonly Vector3[] CORNERS_BUFFER = new Vector3[4];

        public static Rect ImageRectOf(RectTransform rectTransform)
        {
            (Vector2 min, Vector2 max) = ScreenBoundsOf(rectTransform);
            return new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
        }

        public static Vector2 NormalizedCenterOf(Rect imageRect) =>
            new (Mathf.Clamp01(imageRect.center.x / Mathf.Max(1, Screen.width)),
                Mathf.Clamp01(imageRect.center.y / Mathf.Max(1, Screen.height)));

        public static Vector2 ScreenCenterOf(RectTransform rectTransform)
        {
            (Vector2 min, Vector2 max) = ScreenBoundsOf(rectTransform);
            return (min + max) * 0.5f;
        }

        public static Vector2 ImageToScreenPoint(Vector2 imagePoint) =>
            new (imagePoint.x, Screen.height - imagePoint.y);

        public static Vector2 ScreenToImagePoint(Vector2 screenPoint) =>
            new (screenPoint.x, Screen.height - screenPoint.y);

        public static Vector2 NormalizedImageToScreenPoint(Vector2 normalized) =>
            new (normalized.x * Screen.width, (1f - normalized.y) * Screen.height);

        public static Vector2 NormalizedToImagePoint(Vector2 normalized) =>
            new (normalized.x * Screen.width, normalized.y * Screen.height);

        /// <summary>
        ///     RuntimePanelUtils offers no panel-to-screen conversion, so the inverse is recovered from two probe
        ///     conversions.
        /// </summary>
        public static Vector2 PanelToImagePoint(IPanel panel, Vector2 panelPoint)
        {
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 unit = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one);
            Vector2 perPixel = unit - origin;

            return new Vector2(
                Mathf.Approximately(perPixel.x, 0f) ? 0f : (panelPoint.x - origin.x) / perPixel.x,
                Mathf.Approximately(perPixel.y, 0f) ? 0f : (panelPoint.y - origin.y) / perPixel.y);
        }

        /// <summary>
        ///     Built from the same probe conversions as <see cref="PanelToImagePoint" /> so a point round-trips
        ///     exactly, instead of calling ScreenToPanel directly.
        /// </summary>
        public static Vector2 ImageToPanelPoint(IPanel panel, Vector2 imagePoint)
        {
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 unit = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one);
            Vector2 perPixel = unit - origin;

            return new Vector2(origin.x + (imagePoint.x * perPixel.x), origin.y + (imagePoint.y * perPixel.y));
        }

        public static Rect PanelRectToImageRect(IPanel panel, Rect panelRect)
        {
            Vector2 min = PanelToImagePoint(panel, panelRect.min);
            Vector2 max = PanelToImagePoint(panel, panelRect.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static (Vector2 min, Vector2 max) ScreenBoundsOf(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(CORNERS_BUFFER);
            Camera? camera = RenderCameraOf(rectTransform);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var i = 0; i < 4; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, CORNERS_BUFFER[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            return (min, max);
        }

        private static Camera? RenderCameraOf(RectTransform rectTransform)
        {
            Canvas? canvas = rectTransform.GetComponentInParent<Canvas>();

            if (canvas == null)
                return null;

            Canvas rootCanvas = canvas.rootCanvas;
            return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        }
    }
}
