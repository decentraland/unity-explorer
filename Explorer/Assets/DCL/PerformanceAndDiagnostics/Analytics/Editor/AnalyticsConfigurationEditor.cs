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
        private readonly Dictionary<string, bool> groupEnabledStates = new ();

        public override void OnInspectorGUI()
        {
            var config = (AnalyticsConfiguration)target;

            DrawDefaultInspectorExceptGroups();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("ANALYTICS EVENTS", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Events")) { RefreshEvents(config); }
            if (GUILayout.Button("Expand All")) { SetAllFoldoutStates(true); }
            if (GUILayout.Button("Collapse All")) { SetAllFoldoutStates(false); }
            EditorGUILayout.EndHorizontal();

            // Custom inspector for groups
            foreach (AnalyticsConfiguration.AnalyticsGroup group in config.groups)
            {
                foldoutStates.TryAdd(group.groupName, true);

                if (!groupEnabledStates.ContainsKey(group.groupName))
                    groupEnabledStates[group.groupName] = AreAllEventsEnabled(group.events);

                EditorGUILayout.BeginHorizontal();

                bool previousGroupState = groupEnabledStates[group.groupName];
                groupEnabledStates[group.groupName] = EditorGUILayout.Toggle(groupEnabledStates[group.groupName], GUILayout.Width(15));

                var style = new GUIStyle(EditorStyles.boldLabel);
                string groupName = group.groupName.ToUpper();

                if (GUILayout.Button(groupName, style, GUILayout.ExpandWidth(true)))
                    foldoutStates[group.groupName] = !foldoutStates[group.groupName];

                EditorGUILayout.EndHorizontal();

                if (groupEnabledStates[group.groupName] != previousGroupState)
                    SetAllEventsEnabled(group.events, groupEnabledStates[group.groupName]);

                if (foldoutStates[group.groupName])
                {
                    EditorGUI.indentLevel++;

                    foreach (AnalyticsConfiguration.AnalyticsEventToggle toggle in group.events)
                        toggle.isEnabled = EditorGUILayout.ToggleLeft(toggle.eventName, toggle.isEnabled);

                    EditorGUI.indentLevel--;
                }
            }

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
            }
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

            config.groups.RemoveAll(group => !allGroups.ContainsKey(group.groupName));

            foreach (AnalyticsConfiguration.AnalyticsGroup group in config.groups) { group.events.RemoveAll(toggle => !allGroups[group.groupName].Contains(toggle.eventName)); }

            foreach (string groupName in allGroups.Keys)
            {
                AnalyticsConfiguration.AnalyticsGroup group = config.groups.Find(g => g.groupName == groupName);

                if (group == null)
                {
                    group = new AnalyticsConfiguration.AnalyticsGroup { groupName = groupName };
                    config.groups.Add(group);
                }

                foreach (string eventName in allGroups[groupName])
                    if (!group.events.Exists(toggle => toggle.eventName == eventName))
                        group.events.Add(new AnalyticsConfiguration.AnalyticsEventToggle
                        {
                            eventName = eventName,
                            isEnabled = true, // Default to enabled
                        });
            }

            EditorUtility.SetDirty(config);
        }

        private static bool AreAllEventsEnabled(List<AnalyticsConfiguration.AnalyticsEventToggle> events)
        {
            foreach (AnalyticsConfiguration.AnalyticsEventToggle toggle in events)
                if (!toggle.isEnabled)
                    return false;

            return true;
        }

        private void SetAllEventsEnabled(List<AnalyticsConfiguration.AnalyticsEventToggle> events, bool isEnabled)
        {
            foreach (AnalyticsConfiguration.AnalyticsEventToggle toggle in events) { toggle.isEnabled = isEnabled; }
        }

        private void SetAllFoldoutStates(bool state)
        {
            var keys = new List<string>(foldoutStates.Keys);

            foreach (string key in keys) { foldoutStates[key] = state; }
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
