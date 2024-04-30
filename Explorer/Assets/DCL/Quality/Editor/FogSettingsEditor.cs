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
        private SerializedObject serializedObject;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            serializedObject = property.serializedObject;
            m_Fog = property.FindPropertyRelative("m_Fog");
            return new IMGUIContainer(OnIMGUI);
        }

        private void OnIMGUI()
        {
            EditorGUILayout.PropertyField(m_Fog, Styles.FogEnable);
            serializedObject.ApplyModifiedProperties();
        }

        internal class Styles
        {
            public static readonly GUIContent FogEnable = EditorGUIUtility.TrTextContent("Fog", "Specifies whether fog is used in the Scene or not.");
        }
    }
}
