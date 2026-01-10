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

        private void SortSettings()
        {
            if (settings == null || !settings.isArray || settings.arraySize == 0) return;

            var sortedItems = new List<object>();

            for (int i = 0; i < settings.arraySize; i++)
            {
                var element = settings.GetArrayElementAtIndex(i);
                object managedRef = element.managedReferenceValue;
                if (managedRef != null)
                    sortedItems.Add(managedRef);
            }

            if (sortedItems.Count == 0) return;

            bool needsSorting = false;
            for (int i = 0; i < sortedItems.Count - 1; i++)
            {
                Type typeA = sortedItems[i].GetType();
                Type typeB = sortedItems[i + 1].GetType();
                string nameA = typeA.DeclaringType != null ? typeA.DeclaringType.Name + "." + typeA.Name : typeA.Name;
                string nameB = typeB.DeclaringType != null ? typeB.DeclaringType.Name + "." + typeB.Name : typeB.Name;
                if (string.Compare(nameA, nameB, StringComparison.Ordinal) > 0)
                {
                    needsSorting = true;
                    break;
                }
            }

            if (!needsSorting) return;

            sortedItems.Sort((a, b) =>
            {
                Type typeA = a.GetType();
                Type typeB = b.GetType();
                string nameA = typeA.DeclaringType != null ? typeA.DeclaringType.Name + "." + typeA.Name : typeA.Name;
                string nameB = typeB.DeclaringType != null ? typeB.DeclaringType.Name + "." + typeB.Name : typeB.Name;
                return string.Compare(nameA, nameB, StringComparison.Ordinal);
            });

            for (int i = 0; i < sortedItems.Count; i++)
            {
                var element = settings.GetArrayElementAtIndex(i);
                element.managedReferenceValue = sortedItems[i];
            }

            serializedObject.ApplyModifiedProperties();
        }

        private IReadOnlyCollection<Type> GetEligibleSettingsTypes()
        {
            Type targetPluginType;
            Type targetContainerType;

            if (targetObj.GetType() == typeof(GlobalPluginSettingsContainer))
            {
                targetPluginType = typeof(IDCLGlobalPlugin<>);
                targetContainerType = typeof(DCLGlobalContainer<>);
            }
            else if (targetObj.GetType() == typeof(WorldPluginSettingsContainer))
            {
                targetPluginType = typeof(IDCLWorldPlugin<>);
                targetContainerType = typeof(DCLWorldContainer<>);
            }
            else return TypeCache.GetTypesDerivedFrom<IDCLPluginSettings>().Where(AdditionalTypeFiler).ToList();

            // Get their settings types
            var derivedTypes = TypeCache.GetTypesDerivedFrom(targetPluginType).Concat(TypeCache.GetTypesDerivedFrom(targetContainerType));
            var targetCollection = new List<Type>();

            foreach (Type pluginType in derivedTypes.Where(AdditionalTypeFiler))
            {
                Type genericType = pluginType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDCLPlugin<>));
                targetCollection.Add(genericType.GenericTypeArguments[0]);
            }

            return targetCollection;
        }

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            SortSettings();

            var container = new VisualElement();

            var settingsField = new PropertyField(settings);
            settingsField.Bind(serializedObject);
            container.Add(settingsField);

            var addSettingsButton = new Button(() =>
            {
                var menu = new GenericMenu();
                IReadOnlyCollection<Type> types = GetEligibleSettingsTypes();

                foreach (Type type in types.OrderBy(t => t.Name))
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
                        SortSettings();
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
