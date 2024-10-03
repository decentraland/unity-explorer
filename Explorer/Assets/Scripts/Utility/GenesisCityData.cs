using UnityEngine;

namespace Utility
{
    public static class GenesisCityData
    {
        public static readonly Vector2Int MIN_PARCEL = -150 * Vector2Int.one;
        public static readonly Vector2Int MAX_PARCEL = new (163, 158);

        // max parcel if City would be square (but it is not)
        public static readonly Vector2Int MAX_SQUARE_CITY_PARCEL = 150 * Vector2Int.one;

        public static readonly Rect[] INTERACTABLE_WORLD_BOUNDS =
        {
            RectUtils.MinMaxRect(MIN_PARCEL, MAX_SQUARE_CITY_PARCEL),
            Rect.MinMaxRect(62, 151, 162, MAX_PARCEL.y),
            Rect.MinMaxRect(151, 59, MAX_PARCEL.x, 150),
        };

        public static bool IsInsideBounds(float x, float y)
        {
            Vector2 pos = new Vector2(x, y);

            foreach (Rect rect in INTERACTABLE_WORLD_BOUNDS)
                if (rect.Contains(pos))
                    return true;

            return false;
        }
    }
}
