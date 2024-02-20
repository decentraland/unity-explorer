using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Quality
{
    [CustomPropertyDrawer(typeof(QualitySettingsAsset.QualityCustomLevel))]
    public class QualityCustomLevelEditor : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty volumeSettings = property.FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.volumeProfile));
            SerializedProperty fogSettings = property.FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.fogSettings));

            var fsprop = new PropertyField(fogSettings, "Fog Settings");
            fsprop.Bind(property.serializedObject);

            var container = new VisualElement();

            // it should be never null though
            if (volumeSettings.objectReferenceValue != null)
            {
                container.Add(CreateHeader("Volume Profile"));

                var volumeProfileEditor = UnityEditor.Editor.CreateEditor(volumeSettings.objectReferenceValue);
                var imgui = new IMGUIContainer(volumeProfileEditor.OnInspectorGUI);
                container.Add(imgui);
            }

            container.Add(CreateHeader("Fog"));
            container.Add(fsprop);
            return container;
        }

        private Label CreateHeader(string name)
        {
            var header = new Label(name);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 10;
            header.style.marginBottom = 5;
            return header;
        }
    }
}
