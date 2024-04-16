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
            SerializedProperty environmentSettings = property.FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.environmentSettings));

            var fsprop = new PropertyField(fogSettings, "Fog Settings");
            fsprop.Bind(property.serializedObject);

            var environmentSettingsProp = new PropertyField(environmentSettings, "Environment Settings");
            environmentSettingsProp.Bind(property.serializedObject);

            var container = new VisualElement();

            // it should be never null though
            if (volumeSettings.objectReferenceValue != null)
            {
                container.Add(CreateHeader("Volume Profile"));

                var volumeProfileEditor = UnityEditor.Editor.CreateEditor(volumeSettings.objectReferenceValue);
                var imgui = new IMGUIContainer(volumeProfileEditor.OnInspectorGUI);
                container.Add(imgui);
            }

            container.Add(CreateLensFlare(property));

            container.Add(CreateHeader("Fog"));
            container.Add(fsprop);

            container.Add(CreateHeader("Environment"));
            container.Add(environmentSettingsProp);

            return container;
        }

        private VisualElement CreateLensFlare(SerializedProperty property)
        {
            SerializedProperty lensFlareEnabled = property.FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.lensFlareEnabled));
            SerializedProperty lensFlareAsset = property.FindPropertyRelative(nameof(QualitySettingsAsset.QualityCustomLevel.lensFlareComponent));

            var lensFlareEnabledProp = new PropertyField(lensFlareEnabled, "Lens Flare Enabled");
            lensFlareEnabledProp.Bind(property.serializedObject);

            var lensFlareAssetEditor = UnityEditor.Editor.CreateEditor(lensFlareAsset.objectReferenceValue);
            var imgui = new IMGUIContainer(lensFlareAssetEditor.OnInspectorGUI);

            var container = new VisualElement();

            container.Add(CreateHeader("Lens Flare"));

            container.Add(lensFlareEnabledProp);

            lensFlareEnabledProp.RegisterValueChangeCallback(c =>
            {
                if (c.changedProperty.boolValue)
                    container.Add(imgui);
                else if (imgui.parent == container)
                    container.Remove(imgui);
            });

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
