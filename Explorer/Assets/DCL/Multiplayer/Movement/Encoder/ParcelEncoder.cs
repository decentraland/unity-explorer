using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Flatten (x,y) parcel coordinates into 1-dimensional array
    /// </summary>
    public static class ParcelEncoder
    {
        // TODO (Vit): now hardcoded, but it should depend on the Genesis Size + Landscape margins settings
        public const int MIN_X = -152;
        public const int MAX_X = 164;
        public const int MIN_Y = -152;
        public const int MAX_Y = 160;

        private const int WIDTH = MAX_X - MIN_X + 1;

        public static int Encode(Vector2Int parcel) =>
            parcel.x - MIN_X + ((parcel.y - MIN_Y) * WIDTH);

        public static Vector2Int Decode(int index) =>
            new ((index % WIDTH) + MIN_X, (index / WIDTH) + MIN_Y);
    }
}
