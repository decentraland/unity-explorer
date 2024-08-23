using DCL.Gizmos;
using UnityEngine;
using Utility;

namespace ECS.Unity.SceneBoundsChecker
{
    public class GizmosDrawSceneBounds : SceneGizmosProviderBase
    {
        private static readonly Color[] RANDOM_COLORS =
        {
            Color.red,
            Color.blue,
            Color.cyan,
            Color.green,
            Color.magenta,
            Color.yellow,
        };

        private Color pickedColor;
        private Vector3 center;
        private Vector3 size;

        public override void OnInitialize()
        {
            // Pick color based on the scene coordinate
            // so it's possible to distinguish between different scenes
            pickedColor = RANDOM_COLORS[Mathf.Abs(SceneData.SceneShortInfo.BaseParcel.GetHashCode()) % RANDOM_COLORS.Length];

            float sceneHeight = SceneData.Geometry.Height;
            ParcelMathHelper.SceneCircumscribedPlanes planes = SceneData.Geometry.CircumscribedPlanes;

            center = new Vector3(planes.MinX + ((planes.MaxX - planes.MinX) / 2), sceneHeight / 2, planes.MinZ + ((planes.MaxZ - planes.MinZ) / 2));
            size = new Vector3(planes.MaxX - planes.MinX, sceneHeight, planes.MaxZ - planes.MinZ);
        }

        public override void OnDrawGizmosSelected()
        {
            Color color = Gizmos.color;

            Gizmos.color = pickedColor;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = color;
        }
    }
}
