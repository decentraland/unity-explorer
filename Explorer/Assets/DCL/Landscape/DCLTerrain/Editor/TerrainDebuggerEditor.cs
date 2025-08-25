using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Decentraland.Terrain
{
    [CustomEditor(typeof(TerrainDebugger))]
    public sealed class TerrainDebuggerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var target = (TerrainDebugger)this.target;

            if (target.TerrainData == null)
            {
                EditorGUILayout.HelpBox("You must assign terrain data.", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Parcel");
            GUILayout.Space(2);
            int2 parcel = target.Parcel;

            EditorGUILayout.SelectableLabel($"{parcel.x}, {parcel.y}",
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.EndHorizontal();
        }
    }
}
