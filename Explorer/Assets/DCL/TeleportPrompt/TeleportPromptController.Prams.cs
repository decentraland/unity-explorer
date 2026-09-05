using UnityEngine;

namespace DCL.TeleportPrompt
{
    public partial class TeleportPromptController
    {
        public struct Params
        {
            public Vector2Int Coords { get; }

            public Params(Vector2Int coords)
            {
                Coords = coords;
            }
        }
    }
}
