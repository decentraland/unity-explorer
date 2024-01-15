using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController
    {
        public struct Params
        {
            public Vector2Int Coordinate { get; }
            public SceneReadinessReport SceneReadinessReport { get; }

            public Params(Vector2Int coordinate, SceneReadinessReport sceneReadinessReport)
            {
                Coordinate = coordinate;
                SceneReadinessReport = sceneReadinessReport;
            }
        }
    }
}
