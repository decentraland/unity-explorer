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

        /// <summary>
        /// Parses the single /goto argument into a <see cref="GotoTarget" />.
        /// Grammar: "random" | "crowd" | "x,y" | "x,y/spawn" | "world" | "world/x,y".
        /// A spawn point name contains neither ',' nor '/'.
        /// Anything that matches none of the forms is treated verbatim as a world name.
        /// </summary>
        public static GotoTarget ParseGotoTarget(string param)
        {
            if (param == PARAMETER_RANDOM)
                return new GotoTarget(world: null, parcel: null, spawnPoint: null, isRandom: true);

            if (param == PARAMETER_CROWD)
                return new GotoTarget(world: null, parcel: null, spawnPoint: null, isCrowd: true);

            if (IsPositionParameter(param, false))
                return new GotoTarget(world: null, parcel: ParseRawPosition(param), spawnPoint: null);

            int slashIndex = param.IndexOf('/');

            if (slashIndex > 0 && slashIndex < param.Length - 1)
            {
                string head = param.Substring(0, slashIndex);
                string tail = param.Substring(slashIndex + 1);

                if (IsPositionParameter(head, false) && IsSpawnPointName(tail))
                    return new GotoTarget(world: null, parcel: ParseRawPosition(head), spawnPoint: tail);

                if (IsPositionParameter(tail, false))
                    return new GotoTarget(world: head, parcel: ParseRawPosition(tail), spawnPoint: null);
            }

            return new GotoTarget(world: param, parcel: null, spawnPoint: null);
        }

        private static bool IsSpawnPointName(string segment) =>
            segment.Length > 0 && segment.IndexOf(',') < 0 && segment.IndexOf('/') < 0;
    }
}
