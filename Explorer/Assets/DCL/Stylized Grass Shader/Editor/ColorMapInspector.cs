using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StylizedGrass
{
    [CustomEditor(typeof(GrassColorMap))]
    public class ColorMapInspector : Editor
    {
        GrassColorMap colorMap;
        SerializedProperty overrideTexture;
        SerializedProperty texture;
        SerializedProperty customTex;
        SerializedProperty bounds;

        private void OnEnable()
        {
            colorMap = (GrassColorMap)target;

            texture = serializedObject.FindProperty("texture");
            overrideTexture = serializedObject.FindProperty("overrideTexture");
            customTex = serializedObject.FindProperty("customTex");
            bounds = serializedObject.FindProperty("bounds");
        }

        public override bool HasPreviewGUI()
        {
            return colorMap.texture && !overrideTexture.boolValue;
        }
        
        public override void OnInspectorGUI()
        {
            StylizedGrassGUI.DrawHeader();
            
            serializedObject.Update();

            if(GUILayout.Button("Set as active", GUILayout.MaxWidth(150f)))
            {
                colorMap.SetActive();
            }
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Render area", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            {
                EditorGUILayout.PropertyField(bounds);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(overrideTexture);

            if (overrideTexture.boolValue)
            {
                EditorGUILayout.PropertyField(customTex);
            }
            if(colorMap.texture == null)
            {
                EditorGUILayout.HelpBox("No texture has been saved to this asset. Use the ColorMapRenderer component to do this", MessageType.Error);
            }
            //ColorMapEditor.DrawTexturePreview(colorMap, 300f);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (colorMap.texture == null || overrideTexture.boolValue) return;

            GUI.DrawTexture(r, colorMap.texture, ScaleMode.ScaleToFit);

            if (EditorGUIUtility.GetObjectPickerControlID() == -1)
            {
                GUI.Label(new Rect(r.width * 0.5f - (100 * 0.5f), 30, 100, 25), string.Format("{0}x{0}px", colorMap.texture.height), EditorStyles.toolbarButton);
                GUI.Label(new Rect(r.width * 0.5f - (175 * 0.5f), r.height - 5, 175, 25), string.Format("{0} texel(s) per unit", ColorMapRendering.GetTexelSize(colorMap.texture.height, colorMap.bounds.size.x)), EditorStyles.toolbarButton);
            }
        }

        public override void OnPreviewSettings()
        {
            if (colorMap.texture == false) return;

            GUILayout.Label($"Resolution ({colorMap.texture.width}x{colorMap.texture.height})");
        }
    }
}