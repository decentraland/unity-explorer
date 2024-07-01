using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.Editor
{
    [CustomEditor(typeof(AnalyticsConfiguration))]
    public class AnalyticsConfigurationEditor : UnityEditor.Editor
    {
        private readonly Dictionary<string, bool> foldoutStates = new ();

        public override void OnInspectorGUI()
        {
            AnalyticsConfiguration config = (AnalyticsConfiguration)target;

            DrawDefaultInspectorExceptGroups();

            if (GUILayout.Button("Refresh Events"))
                RefreshEvents(config);

            // Custom inspector for groups
            foreach (var group in config.groups)
            {
                foldoutStates.TryAdd(group.groupName, true);
                foldoutStates[group.groupName] = EditorGUILayout.Foldout(foldoutStates[group.groupName], group.groupName);

                if (foldoutStates[group.groupName])
                {
                    EditorGUI.indentLevel++;
                    foreach (var toggle in group.events)
                        toggle.isEnabled = EditorGUILayout.ToggleLeft(toggle.eventName, toggle.isEnabled);

                    EditorGUI.indentLevel--;
                }
            }

            if (GUI.changed)
                EditorUtility.SetDirty(config);
        }

        private void DrawDefaultInspectorExceptGroups()
        {
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true);

            while (property.NextVisible(false))
                if (property.name != "groups")
                    EditorGUILayout.PropertyField(property, true);

            serializedObject.ApplyModifiedProperties();
        }

        private void RefreshEvents(AnalyticsConfiguration config)
        {
            Dictionary<string, List<string>> allGroups = GetAnalyticsEvents();

            // Remove old groups and events that no longer exist
            config.groups.RemoveAll(group => !allGroups.ContainsKey(group.groupName));

            foreach (AnalyticsConfiguration.AnalyticsGroup group in config.groups) { group.events.RemoveAll(toggle => !allGroups[group.groupName].Contains(toggle.eventName)); }

            // Add new groups and events
            foreach (string groupName in allGroups.Keys)
            {
                AnalyticsConfiguration.AnalyticsGroup group = config.groups.Find(g => g.groupName == groupName);

                if (group == null)
                {
                    group = new AnalyticsConfiguration.AnalyticsGroup { groupName = groupName };
                    config.groups.Add(group);
                }

                foreach (string eventName in allGroups[groupName])
                {
                    if (!group.events.Exists(toggle => toggle.eventName == eventName))
                    {
                        group.events.Add(new AnalyticsConfiguration.AnalyticsEventToggle
                        {
                            eventName = eventName,
                            isEnabled = true, // Default to enabled
                        });
                    }
                }
            }

            EditorUtility.SetDirty(config);
        }

        private static Dictionary<string, List<string>> GetAnalyticsEvents()
        {
            var result = new Dictionary<string, List<string>>();

            Type[] nestedTypes = typeof(AnalyticsEvents).GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

            foreach (Type nestedType in nestedTypes)
            {
                FieldInfo[] fields = nestedType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                var eventNames = (from field in fields
                    where field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string)
                    select (string)field.GetRawConstantValue()).ToList();

                result[nestedType.Name] = eventNames;
            }

            return result;
        }
    }
}
