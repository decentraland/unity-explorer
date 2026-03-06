using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Resolves screen-space spawn positions (pixels) from UI <see cref="RectTransform"/> elements
    /// and clamps live particle positions to a defined screen-space lane. No camera required —
    /// all coordinates are in pixel space.
    /// </summary>
    public sealed class UIReactionSpawnResolver
    {
        private readonly RectTransform laneRect;
        private readonly Camera uiCamera;
        private readonly Vector3[] corners = new Vector3[4];

        private Rect cachedLaneRect;
        private int cachedFrame = -1;

        public UIReactionSpawnResolver(RectTransform laneRect)
        {
            this.laneRect = laneRect;
            uiCamera = laneRect.GetComponentInParent<Canvas>()?.worldCamera;
        }

        /// <summary>Screen position (pixels) at the center of the given rect.</summary>
        public Vector2 GetSpawnPxFromRectCenter(RectTransform rect)
        {
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(uiCamera, worldCenter);
        }

        /// <summary>Screen position (pixels) at the bottom 12% of the lane rect.</summary>
        public Vector2 GetSpawnPxBottomCenter()
        {
            Rect r = GetLaneScreenRect();
            return new Vector2(r.center.x, Mathf.Lerp(r.yMin, r.yMax, 0.12f));
        }

        /// <summary>
        /// Clamps a particle's screen position (pixels) to the lane's screen rect.
        /// </summary>
        public void ClampToLane(ref Vector2 screenPos)
        {
            Rect lane = GetLaneScreenRect();
            screenPos.x = Mathf.Clamp(screenPos.x, lane.xMin, lane.xMax);
            screenPos.y = Mathf.Clamp(screenPos.y, lane.yMin, lane.yMax);
        }

        private Rect GetLaneScreenRect()
        {
            int frame = Time.frameCount;
            if (frame == cachedFrame)
                return cachedLaneRect;

            cachedFrame = frame;
            laneRect.GetWorldCorners(corners);

            Vector2 a = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

            cachedLaneRect = Rect.MinMaxRect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

            return cachedLaneRect;
        }
    }
}
