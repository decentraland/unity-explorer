using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Resolves world-space spawn positions from UI <see cref="RectTransform"/> elements
    /// and clamps live particle positions to a defined screen-space lane.
    /// Uses <c>Camera.main</c> lazily — safe to construct before the camera is live,
    /// as the camera is always available by the time users can trigger reactions.
    /// </summary>
    public sealed class UIReactionSpawnResolver
    {
        private readonly RectTransform laneRect;
        private readonly float depthFromCamera;

        public UIReactionSpawnResolver(RectTransform laneRect, float depthFromCamera)
        {
            this.laneRect = laneRect;
            this.depthFromCamera = depthFromCamera;
        }

        /// <summary>World-space center of a rect, placed at <see cref="depthFromCamera"/> in front of the camera.</summary>
        public bool TryGetSpawnPosFromRectCenter(RectTransform rect, out Vector3 worldPos)
        {
            var cam = Camera.main;

            if (cam == null)
            {
                worldPos = Vector3.zero;
                return false;
            }

            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
            worldPos = ScreenToWorld(cam, screen);
            return true;
        }

        /// <summary>World-space position at the bottom 12% of the lane rect.</summary>
        public bool TryGetSpawnPosBottomCenter(out Vector3 worldPos)
        {
            var cam = Camera.main;

            if (cam == null)
            {
                worldPos = Vector3.zero;
                return false;
            }

            Rect r = GetLaneScreenRect();
            Vector2 screen = new Vector2(r.center.x, Mathf.Lerp(r.yMin, r.yMax, 0.12f));
            worldPos = ScreenToWorld(cam, screen);
            return true;
        }

        /// <summary>
        /// Clamps a world-space particle position to the lane's screen-space rect.
        /// Returns <c>false</c> if no camera is available (position unchanged).
        /// </summary>
        public bool ClampToLane(ref Vector3 worldPos)
        {
            var cam = Camera.main;
            if (cam == null) return false;

            Rect lane = GetLaneScreenRect();
            Vector3 sp = cam.WorldToScreenPoint(worldPos);

            float cx = Mathf.Clamp(sp.x, lane.xMin, lane.xMax);
            float cy = Mathf.Clamp(sp.y, lane.yMin, lane.yMax);

            if (Mathf.Approximately(sp.x, cx) && Mathf.Approximately(sp.y, cy))
                return false;

            sp.x = cx;
            sp.y = cy;
            sp.z = depthFromCamera;
            worldPos = cam.ScreenToWorldPoint(sp);
            return true;
        }

        /// <summary>Camera-space right and up vectors for jitter at spawn time.</summary>
        public bool TryGetCameraAxes(out Vector3 right, out Vector3 up)
        {
            var cam = Camera.main;

            if (cam == null)
            {
                right = Vector3.right;
                up = Vector3.up;
                return false;
            }

            right = cam.transform.right;
            up = cam.transform.up;
            return true;
        }

        private Rect GetLaneScreenRect()
        {
            Vector3[] corners = new Vector3[4];
            laneRect.GetWorldCorners(corners);

            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

            return Rect.MinMaxRect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private Vector3 ScreenToWorld(Camera cam, Vector2 screen) =>
            cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depthFromCamera));
    }
}
