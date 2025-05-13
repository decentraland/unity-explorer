using UnityEngine;

namespace DCL.Chat.Commands
{
    public static class ChatParamUtils
    {
        public const string PARAMETER_RANDOM = "random";
        public const string PARAMETER_CROWD = "crowd";

        /// <summary>
        /// Checks if a parameter is a valid position (x,y) or a special case (random, crowd).
        /// </summary>
        /// <param name="param">The parameter to check</param>
        /// <param name="allowSpecial">Also allow random and crowd strings</param>
        public static bool IsPositionParameter(string param, bool allowSpecial)
        {
            if (allowSpecial && param is PARAMETER_RANDOM or PARAMETER_CROWD)
                return true;

            string[] coords = param.Split(',');
            return coords.Length == 2 && int.TryParse(coords[0], out _) && int.TryParse(coords[1], out _);
        }

        /// <summary>
        /// Parses a raw string position "x,y" into a Vector2Int.
        /// </summary>
        public static Vector2Int ParseRawPosition(string param)
        {
            string[] coords = param.Split(',');
            return new Vector2Int(int.Parse(coords[0]), int.Parse(coords[1]));
        }
    }
}
