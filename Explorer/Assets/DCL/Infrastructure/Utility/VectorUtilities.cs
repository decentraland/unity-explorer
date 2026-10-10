using UnityEngine;

namespace Utility
{
    public static class VectorUtilities
    {
        public static Vector2 OneHalf = new (0.5f, 0.5f);

        public static bool TryParseVector2Int(string input, out Vector2Int result)
        {
            result = Vector2Int.zero;
            string[] components = input.Split(',');

            if (components.Length != 2) return false;
            if (!int.TryParse(components[0], out int x) || !int.TryParse(components[1], out int y)) return false;

            result = new Vector2Int(x, y);
            return true;

        }
    }
}
