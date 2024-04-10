using UnityEngine;

namespace Utility
{
    public static class GenesisCityData
    {
        public static readonly Vector2Int MIN_PARCEL = -150 * Vector2Int.one;

        // max parcel if City would be square (but it is not)
        public static readonly Vector2Int MAX_SQUARE_CITY_PARCEL = 150 * Vector2Int.one;

        public static readonly Rect[] INTERACTABLE_WORLD_BOUNDS =
        {
            RectUtils.MinMaxRect(MIN_PARCEL, MAX_SQUARE_CITY_PARCEL),
            Rect.MinMaxRect(62, 151, 162, 158),
            Rect.MinMaxRect(151, 59, 163, 150),
        };
    }
}
