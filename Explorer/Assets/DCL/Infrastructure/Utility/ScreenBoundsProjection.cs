using UnityEngine;

namespace Utility
{
    /// <summary>
    ///     Projects world-space bounds onto a camera's viewport: the box's visible part is its corners in front of
    ///     the near plane plus the points where its edges cross that plane, so a box straddling the lens still
    ///     projects to the area it covers instead of an inverted rectangle.
    /// </summary>
    public static class ScreenBoundsProjection
    {
        /// <summary>Corners of an axis-aligned box; also one past the highest axis bit of a corner index.</summary>
        private const int BOX_CORNER_COUNT = 8;

        /// <summary>
        ///     The viewport-space rectangle (origin bottom-left, not clamped to 0..1) spanned by the visible part of
        ///     the bounds. False when nothing of the box lies in front of the near plane.
        /// </summary>
        public static bool TryProjectToViewport(Camera camera, in Bounds bounds, out Rect viewportRect)
        {
            Transform cameraTransform = camera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 cameraForward = cameraTransform.forward;
            float nearPlane = camera.nearClipPlane;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var projectedAnyPoint = false;

            // Each bit of a corner index picks the low or the high side of an axis.
            for (var corner = 0; corner < BOX_CORNER_COUNT; corner++)
            {
                Vector3 from = GetCorner(in bounds, corner);
                float fromDepth = Vector3.Dot(from - cameraPosition, cameraForward);

                if (fromDepth >= nearPlane)
                    Include(from);

                for (var axisBit = 1; axisBit < BOX_CORNER_COUNT; axisBit <<= 1)
                {
                    if ((corner & axisBit) != 0) continue;

                    Vector3 to = GetCorner(in bounds, corner | axisBit);
                    float toDepth = Vector3.Dot(to - cameraPosition, cameraForward);

                    if ((fromDepth >= nearPlane) != (toDepth >= nearPlane))
                        Include(Vector3.Lerp(from, to, (fromDepth - nearPlane) / (fromDepth - toDepth)));
                }
            }

            if (!projectedAnyPoint)
            {
                viewportRect = Rect.zero;
                return false;
            }

            viewportRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            return true;

            void Include(Vector3 world)
            {
                Vector3 viewportPoint = camera.WorldToViewportPoint(world);
                min = Vector2.Min(min, viewportPoint);
                max = Vector2.Max(max, viewportPoint);
                projectedAnyPoint = true;
            }
        }

        private static Vector3 GetCorner(in Bounds bounds, int corner)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z);
        }
    }
}
