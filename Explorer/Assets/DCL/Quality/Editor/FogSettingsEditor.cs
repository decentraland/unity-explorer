using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Quality
{
    /// <summary>
    ///     Inherits logic from the Unity's default fog editor
    /// </summary>
    [CustomPropertyDrawer(typeof(FogSettings))]
    public class FogPropertyDrawer : PropertyDrawer
    {
        protected SerializedProperty m_Fog;
        protected SerializedProperty m_FogColor;
        protected SerializedProperty m_FogDensity;
        protected SerializedProperty m_FogMode;
        protected SerializedProperty m_LinearFogEnd;
        protected SerializedProperty m_LinearFogStart;

        private SerializedObject serializedObject;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            serializedObject = property.serializedObject;

            m_Fog = property.FindPropertyRelative("m_Fog");
            m_FogColor = property.FindPropertyRelative("m_FogColor");
            m_FogMode = property.FindPropertyRelative("m_FogMode");
            m_FogDensity = property.FindPropertyRelative("m_FogDensity");
            m_LinearFogStart = property.FindPropertyRelative("m_LinearFogStart");
            m_LinearFogEnd = property.FindPropertyRelative("m_LinearFogEnd");

            return new IMGUIContainer(OnIMGUI);
        }

        private void OnIMGUI()
        {
            EditorGUILayout.PropertyField(m_Fog, Styles.FogEnable);

            if (m_Fog.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_FogColor, Styles.FogColor);
                EditorGUILayout.PropertyField(m_FogMode, Styles.FogMode);

                if ((FogMode)m_FogMode.intValue != FogMode.Linear) { EditorGUILayout.PropertyField(m_FogDensity, Styles.FogDensity); }
                else
                {
                    EditorGUILayout.PropertyField(m_LinearFogStart, Styles.FogLinearStart);
                    EditorGUILayout.PropertyField(m_LinearFogEnd, Styles.FogLinearEnd);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            serializedObject.ApplyModifiedProperties();
        }

        internal class Styles
        {
            public static readonly GUIContent FogDensity = EditorGUIUtility.TrTextContent("Density", "Controls the density of the fog effect in the Scene when using Exponential or Exponential Squared modes.");
            public static readonly GUIContent FogLinearStart = EditorGUIUtility.TrTextContent("Start", "Controls the distance from the camera where the fog will start in the Scene.");
            public static readonly GUIContent FogLinearEnd = EditorGUIUtility.TrTextContent("End", "Controls the distance from the camera where the fog will completely obscure objects in the Scene.");
            public static readonly GUIContent FogEnable = EditorGUIUtility.TrTextContent("Fog", "Specifies whether fog is used in the Scene or not.");
            public static readonly GUIContent FogColor = EditorGUIUtility.TrTextContent("Color", "Controls the color of the fog drawn in the Scene.");
            public static readonly GUIContent FogMode = EditorGUIUtility.TrTextContent("Mode", "Controls the mathematical function determining the way fog accumulates with distance from the camera. Options are Linear, Exponential, and Exponential Squared.");
        }
    }
}
