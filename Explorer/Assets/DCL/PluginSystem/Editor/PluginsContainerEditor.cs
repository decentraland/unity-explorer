using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.PluginSystem.Editor
{
    [CustomEditor(typeof(PluginSettingsContainer), true)]
    public class PluginsContainerEditor : UnityEditor.Editor
    {
        private SerializedProperty settings;
        private PluginSettingsContainer targetObj;

        public Func<Type, bool> AdditionalTypeFiler { private get; set; } = static _ => true;

        private void OnEnable()
        {
            settings = serializedObject.FindProperty(nameof(PluginSettingsContainer.settings));
            targetObj = (PluginSettingsContainer)target;
        }

        private IReadOnlyCollection<Type> GetEligibleSettingsTypes()
        {
            Type targetType;

            if (targetObj.GetType() == typeof(GlobalPluginSettingsContainer))
                targetType = typeof(IDCLGlobalPlugin<>);
            else if (targetObj.GetType() == typeof(WorldPluginSettingsContainer))
                targetType = typeof(IDCLWorldPlugin<>);
            else return TypeCache.GetTypesDerivedFrom<IDCLPluginSettings>().Where(AdditionalTypeFiler).ToList();

            // Get their settings types
            TypeCache.TypeCollection derivedTypes = TypeCache.GetTypesDerivedFrom(targetType);
            var targetCollection = new List<Type>();

            foreach (Type pluginType in derivedTypes.Where(AdditionalTypeFiler))
            {
                Type genericType = pluginType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == targetType);
                targetCollection.Add(genericType.GenericTypeArguments[0]);
            }

            return targetCollection;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();

            var settingsField = new PropertyField(settings);
            settingsField.Bind(serializedObject);
            container.Add(settingsField);

            var addSettingsButton = new Button(() =>
            {
                var menu = new GenericMenu();
                IReadOnlyCollection<Type> types = GetEligibleSettingsTypes();

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
