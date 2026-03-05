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

        public UIReactionSpawnResolver(RectTransform laneRect)
        {
            this.laneRect = laneRect;
            uiCamera = laneRect.GetComponentInParent<Canvas>()?.worldCamera;
        }

        /// <summary>Screen position (pixels) at the center of the given rect.</summary>
        public bool TryGetSpawnPxFromRectCenter(RectTransform rect, out Vector2 px)
        {
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            px = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCenter);
            return true;
        }

        /// <summary>Screen position (pixels) at the bottom 12% of the lane rect.</summary>
        public bool TryGetSpawnPxBottomCenter(out Vector2 px)
        {
            Rect r = GetLaneScreenRect();
            px = new Vector2(r.center.x, Mathf.Lerp(r.yMin, r.yMax, 0.12f));
            return true;
        }

        /// <summary>
        /// Clamps a particle's screen position (pixels) to the lane's screen rect.
        /// Returns <c>true</c> if the position was changed.
        /// </summary>
        public bool ClampToLane(ref Vector2 screenPos)
        {
            Rect lane = GetLaneScreenRect();
            float cx = Mathf.Clamp(screenPos.x, lane.xMin, lane.xMax);
            float cy = Mathf.Clamp(screenPos.y, lane.yMin, lane.yMax);

            if (Mathf.Approximately(screenPos.x, cx) && Mathf.Approximately(screenPos.y, cy))
                return false;

            screenPos.x = cx;
            screenPos.y = cy;
            return true;
        }

        private Rect GetLaneScreenRect()
        {
            Vector3[] corners = new Vector3[4];
            laneRect.GetWorldCorners(corners);

            Vector2 a = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

            return Rect.MinMaxRect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
