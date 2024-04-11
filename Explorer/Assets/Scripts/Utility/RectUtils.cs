using UnityEngine;

namespace Utility
{
    public static class RectUtils
    {
        public static Rect MinMaxRect(Vector2Int min, Vector2Int max) =>
            Rect.MinMaxRect(min.x, min.y, max.x, max.y);

        public static Rect MinMaxRect(Vector2 min, Vector2 max) =>
            Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
