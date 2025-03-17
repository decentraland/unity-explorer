using UnityEngine;

namespace Utility
{
    public static class GenesisCityData
    {
        public static readonly Vector2Int MIN_PARCEL = -150 * Vector2Int.one;
        public static readonly Vector2Int MAX_PARCEL = new (163, 158);
        public static readonly Vector2Int EXTENTS = MAX_PARCEL - MIN_PARCEL;

        // max parcel if City would be square (but it is not)
        public static readonly Vector2Int MAX_SQUARE_CITY_PARCEL = 150 * Vector2Int.one;

        public static readonly Rect[] INTERACTABLE_WORLD_BOUNDS =
        {
            RectUtils.MinMaxRect(MIN_PARCEL, MAX_SQUARE_CITY_PARCEL),
            Rect.MinMaxRect(62, 151, 162, MAX_PARCEL.y),
            Rect.MinMaxRect(151, 59, MAX_PARCEL.x, 150),
        };

        // We need to increase the values of the world bounds by 1 in each direction, otherwise the Contains()
        // method excludes the limit value, so 150,150 for example, will return false
        private static readonly Rect[] EXTENDED_WORLD_BOUNDS =
        {
            Rect.MinMaxRect(MIN_PARCEL.x - 1,MIN_PARCEL.y - 1, MAX_SQUARE_CITY_PARCEL.x + 1, MAX_SQUARE_CITY_PARCEL.y + 1),
            Rect.MinMaxRect(62 - 1, 151 - 1, 162 + 1, MAX_PARCEL.y + 1),
            Rect.MinMaxRect(151 - 1, 59 -1 , MAX_PARCEL.x +1 , 150 + 1),
        };


        public static bool IsInsideBounds(float x, float y)
        {
            Vector2 pos = new Vector2(x, y);

            foreach (Rect rect in EXTENDED_WORLD_BOUNDS)
                if (rect.Contains(pos))
                    return true;

            return false;
        }
    }
}
