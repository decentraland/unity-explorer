using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController
    {
        public struct Params
        {
            public Vector2Int Coordinate { get; }

            public Params(Vector2Int coordinate)
            {
                Coordinate = coordinate;
            }
        }
    }
}
