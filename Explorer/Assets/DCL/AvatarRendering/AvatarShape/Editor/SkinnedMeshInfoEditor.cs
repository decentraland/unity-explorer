using DCL.AvatarRendering.AvatarShape.Helpers;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Editor
{
    [CustomEditor(typeof(SkinnedMeshInfo))]
    public sealed class SkinnedMeshInfoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var renderer = ((SkinnedMeshInfo)target).GetComponent<SkinnedMeshRenderer>();
            Transform[] bones = renderer.bones;
            Mesh mesh = renderer.sharedMesh;
            using NativeArray<byte> bonesPerVertex = mesh.GetBonesPerVertex();
            using NativeArray<BoneWeight1> boneWeights = mesh.GetAllBoneWeights();
            var verticesPerBone = new int[bones.Length];
            bool eventIsRepaint = Event.current.type == EventType.Repaint;

            for (int i = 0, boneWeightIndex = 0; i < bonesPerVertex.Length; i++)
            for (int j = bonesPerVertex[i] - 1; j >= 0; j--, boneWeightIndex++)
                verticesPerBone[boneWeights[boneWeightIndex].boneIndex]++;

            EditorGUILayout.LabelField("Bone Name", "Affected Vertex Count");

            for (var i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                GUIStyle labelStyle;
                string labelText;

                if (bone != null)
                {
                    labelStyle = EditorStyles.label;
                    labelText = bone.name;
                }
                else
                {
                    labelStyle = italicLabel;
                    labelText = "null";
                }

                Rect position = EditorGUILayout.GetControlRect(true);

                if (GUI.Button(GetLabelPosition(position), labelText, labelStyle) && bone != null)
                    EditorGUIUtility.PingObject(bone);

                if (eventIsRepaint)
                {
                    position = GetFieldPosition(position);
                    int id = GUIUtility.GetControlID(FocusType.Passive, position);
                    EditorStyles.label.Draw(position, GUIContent(verticesPerBone[i].ToString()), id);
                }
            }
        }

        private static readonly GUIContent GUI_CONTENT = new ();
        private static GUIStyle italicLabelValue;

        private static Rect GetFieldPosition(Rect position) =>
            new (position.x + EditorGUIUtility.labelWidth + prefixPaddingRight,
                position.y,
                position.width - EditorGUIUtility.labelWidth - prefixPaddingRight,
                position.height);

        private static Rect GetLabelPosition(Rect position) =>
            new (position.x + indent,
                position.y,
                EditorGUIUtility.labelWidth - indent,
                EditorGUIUtility.singleLineHeight);

        private static GUIContent GUIContent(string text)
        {
            GUI_CONTENT.text = text;
            return GUI_CONTENT;
        }

        private static float indent => EditorGUI.indentLevel * indentPerLevel;

        private static float indentPerLevel => 15f;

        private static GUIStyle italicLabel
        {
            get
            {
                italicLabelValue ??= new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Italic,
                };

                return italicLabelValue;
            }
        }

        private static float prefixPaddingRight => 2f;
    }
}
