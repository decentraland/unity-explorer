using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.PluginSystem.Editor
{
    [CustomEditor(typeof(PluginSettingsContainer))]
    public class PluginsContainerEditor : UnityEditor.Editor
    {
        private SerializedProperty settings;
        private PluginSettingsContainer targetObj;

        private void OnEnable()
        {
            settings = serializedObject.FindProperty(nameof(PluginSettingsContainer.settings));
            targetObj = (PluginSettingsContainer)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();

            var settingsField = new PropertyField(settings);
            container.Add(settingsField);

            var addSettingsButton = new Button(() =>
            {
                var menu = new GenericMenu();
                TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<IDCLPluginSettings>();

                foreach (Type type in types)
                {
                    if (type == typeof(NoExposedPluginSettings)) continue;

                    try
                    {
                        if (targetObj.settings.Find(x => x.GetType() == type) != null) continue;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        continue;
                    }

                    menu.AddItem(new GUIContent(type.FullName), false, () =>
                    {
                        object newSettings = Activator.CreateInstance(type);
                        settings.arraySize++;
                        settings.GetArrayElementAtIndex(settings.arraySize - 1).managedReferenceValue = newSettings;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.ShowAsContext();
            });

            addSettingsButton.text = "Add Settings";

            container.Add(addSettingsButton);

            return container;
        }
    }
}
