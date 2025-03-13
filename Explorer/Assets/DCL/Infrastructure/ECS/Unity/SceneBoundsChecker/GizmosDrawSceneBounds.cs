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
        private int parcelCount;
        private float sceneHeight;

#if UNITY_EDITOR
        private GUIStyle labelStyle = new();
#endif

        public override void OnInitialize()
        {
            // Pick color based on the scene coordinate
            // so it's possible to distinguish between different scenes
            pickedColor = RANDOM_COLORS[Mathf.Abs(SceneData.SceneShortInfo.BaseParcel.GetHashCode()) % RANDOM_COLORS.Length];

            parcelCount = SceneData.Parcels.Count;
            sceneHeight = SceneData.Geometry.Height;
            ParcelMathHelper.SceneCircumscribedPlanes planes = SceneData.Geometry.CircumscribedPlanes;

            center = new Vector3(planes.MinX + ((planes.MaxX - planes.MinX) / 2), sceneHeight / 2, planes.MinZ + ((planes.MaxZ - planes.MinZ) / 2));
            size = new Vector3(planes.MaxX - planes.MinX, sceneHeight, planes.MaxZ - planes.MinZ);

#if UNITY_EDITOR
            labelStyle.normal.textColor = pickedColor;
#endif
        }

        public override void OnDrawGizmosSelected()
        {
            Color color = Gizmos.color;

            Gizmos.color = pickedColor;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = color;

#if UNITY_EDITOR
            Vector3 gizmoUpperCornerPosition = center + size * 0.5f + new Vector3(0.0f, 0.5f, 0.0f);
            UnityEditor.Handles.Label(gizmoUpperCornerPosition, $"Parcels: {parcelCount} -> Height: {sceneHeight}", labelStyle);
#endif
        }
    }
}
