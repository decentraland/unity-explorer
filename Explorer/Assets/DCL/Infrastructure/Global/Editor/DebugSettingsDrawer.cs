using DCL.WebRequests.ChromeDevtool;
using Global.AppArgs;
using Global.Dynamic.DebugSettings;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Global.Editor
{
    [CustomPropertyDrawer(typeof(DebugSettings))]
    public class DebugSettingsDrawer : PropertyDrawer
    {
        private static readonly string DEFAULT_CREATOR_HUB_PATH = CreatorHubBrowser.DEFAULT_CREATOR_HUB_BIN_PATH;

        private static float SingleLineHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position.height = SingleLineHeight;

            SerializedProperty appParameters = property.FindPropertyRelative("appParameters");

            bool cdpEnabled = HasFlag(appParameters, AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START);
            string? currentCreatorHubPath = GetFlagValue(appParameters, AppArgsFlags.CREATOR_HUB_BIN_PATH);

            string detectedCreatorHubPath = FindCreatorHubPath();
            bool creatorHubInstalled = !string.IsNullOrEmpty(detectedCreatorHubPath);

            // CDP DevTools Section
            EditorGUI.LabelField(position, "Chrome DevTools Protocol", EditorStyles.boldLabel);
            position.y += SingleLineHeight;

            // Creator Hub status
            var statusStyle = new GUIStyle(EditorStyles.label);

            if (creatorHubInstalled)
            {
                statusStyle.normal.textColor = new Color(0.2f, 0.7f, 0.2f);
                EditorGUI.LabelField(position, $"Creator Hub found: {detectedCreatorHubPath}", statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = new Color(0.9f, 0.5f, 0.1f);
                EditorGUI.LabelField(position, "Creator Hub not found at default location", statusStyle);
            }

            position.y += SingleLineHeight;

            // Enable CDP toggle
            EditorGUI.BeginChangeCheck();
            bool newCdpEnabled = EditorGUI.Toggle(position, new GUIContent("Enable DevTools on Start", "Launches Chrome DevTools Protocol bridge when Play mode starts"), cdpEnabled);

            if (EditorGUI.EndChangeCheck() && newCdpEnabled != cdpEnabled)
            {
                if (newCdpEnabled)
                    EnableCdpDevTools(appParameters, detectedCreatorHubPath);
                else
                    DisableCdpDevTools(appParameters);
            }

            position.y += SingleLineHeight;

            // Custom Creator Hub path (only if CDP is enabled)
            if (cdpEnabled)
            {
                EditorGUI.BeginChangeCheck();

                Rect pathFieldRect = position;
                pathFieldRect.width -= 80;

                Rect browseButtonRect = position;
                browseButtonRect.x = position.xMax - 75;
                browseButtonRect.width = 75;

                string displayPath = currentCreatorHubPath ?? detectedCreatorHubPath ?? "Not found";
                string newPath = EditorGUI.TextField(pathFieldRect, new GUIContent("Creator Hub Path"), displayPath);

                if (GUI.Button(browseButtonRect, "Browse"))
                {
                    string initialDir = !string.IsNullOrEmpty(currentCreatorHubPath) ? Path.GetDirectoryName(currentCreatorHubPath) :
                        !string.IsNullOrEmpty(detectedCreatorHubPath) ? Path.GetDirectoryName(detectedCreatorHubPath) : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

#if UNITY_EDITOR_WIN
                    string selectedPath = EditorUtility.OpenFilePanel("Select Creator Hub Executable", initialDir ?? "", "exe");
#else
                    string selectedPath = EditorUtility.OpenFilePanel("Select Creator Hub Executable", initialDir ?? "", "");
#endif

                    if (!string.IsNullOrEmpty(selectedPath))
                        newPath = selectedPath;
                }

                if (EditorGUI.EndChangeCheck() && newPath != displayPath)
                    SetCreatorHubPath(appParameters, newPath);

                position.y += SingleLineHeight;

                // Clear custom path button if a custom path is set
                if (!string.IsNullOrEmpty(currentCreatorHubPath))
                {
                    Rect clearButtonRect = position;
                    clearButtonRect.x = position.xMax - 150;
                    clearButtonRect.width = 150;

                    if (GUI.Button(clearButtonRect, "Use Default Path"))
                        RemoveFlag(appParameters, AppArgsFlags.CREATOR_HUB_BIN_PATH);

                    position.y += SingleLineHeight;
                }
            }

            // Separator
            position.y += 5;
            EditorGUI.LabelField(position, "", GUI.skin.horizontalSlider);
            position.y += SingleLineHeight;

            // Draw all other properties (including appParameters as raw array)
            position = DrawDefaultProperties(position, property);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty appParameters = property.FindPropertyRelative("appParameters");
            bool cdpEnabled = HasFlag(appParameters, AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START);
            string? currentCreatorHubPath = GetFlagValue(appParameters, AppArgsFlags.CREATOR_HUB_BIN_PATH);

            // CDP section: header + status + toggle
            int lineCount = 3;

            // Custom path field (if CDP enabled)
            if (cdpEnabled)
            {
                lineCount += 1;

                // Clear button (if custom path is set)
                if (!string.IsNullOrEmpty(currentCreatorHubPath))
                    lineCount += 1;
            }

            // Separator
            lineCount += 1;

            // Default properties (including appParameters as raw array)
            float defaultPropertiesHeight = GetDefaultPropertiesHeight(property);

            return (lineCount * SingleLineHeight) + 5 + defaultPropertiesHeight;
        }

        private static Rect DrawDefaultProperties(Rect position, SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;

                float propertyHeight = EditorGUI.GetPropertyHeight(iterator, true);
                position.height = propertyHeight;
                EditorGUI.PropertyField(position, iterator, true);
                position.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            return position;
        }

        private static float GetDefaultPropertiesHeight(SerializedProperty property)
        {
            float height = 0;
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private static string? FindCreatorHubPath()
        {
            // Check default path
            if (File.Exists(DEFAULT_CREATOR_HUB_PATH))
                return DEFAULT_CREATOR_HUB_PATH;

#if UNITY_EDITOR_WIN

            // Search common Windows installation locations
            string[] searchPaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "creator-hub", "Decentraland Creator Hub.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "creator-hub", "Decentraland Creator Hub.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "creator-hub", "Decentraland Creator Hub.exe"),
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }
#else
            // Search common macOS locations
            string[] searchPaths =
            {
                "/Applications/Decentraland Creator Hub.app/Contents/MacOS/Decentraland Creator Hub",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Decentraland Creator Hub.app", "Contents", "MacOS", "Decentraland Creator Hub"),
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }
#endif

            return null;
        }

        private static string FormatFlag(string flag) =>
            $"--{flag}";

        private static bool HasFlag(SerializedProperty appParameters, string flag)
        {
            string formattedFlag = FormatFlag(flag);

            for (int i = 0; i < appParameters.arraySize; i++)
            {
                if (appParameters.GetArrayElementAtIndex(i).stringValue == formattedFlag)
                    return true;
            }

            return false;
        }

        private static string? GetFlagValue(SerializedProperty appParameters, string flag)
        {
            string formattedFlag = FormatFlag(flag);

            for (int i = 0; i < appParameters.arraySize; i++)
            {
                if (appParameters.GetArrayElementAtIndex(i).stringValue == formattedFlag && i + 1 < appParameters.arraySize)
                    return appParameters.GetArrayElementAtIndex(i + 1).stringValue;
            }

            return null;
        }

        private static void EnableCdpDevTools(SerializedProperty appParameters, string? creatorHubPath)
        {
            if (!HasFlag(appParameters, AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START))
            {
                int insertIndex = appParameters.arraySize;

                // Add --flag
                appParameters.InsertArrayElementAtIndex(insertIndex);
                appParameters.GetArrayElementAtIndex(insertIndex).stringValue = FormatFlag(AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START);

                // Add value
                appParameters.InsertArrayElementAtIndex(insertIndex + 1);
                appParameters.GetArrayElementAtIndex(insertIndex + 1).stringValue = "true";
            }

            // Add Creator Hub path if found
            if (!string.IsNullOrEmpty(creatorHubPath))
                SetCreatorHubPath(appParameters, creatorHubPath);

            appParameters.serializedObject.ApplyModifiedProperties();
        }

        private static void DisableCdpDevTools(SerializedProperty appParameters)
        {
            RemoveFlag(appParameters, AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START);
            RemoveFlag(appParameters, AppArgsFlags.CREATOR_HUB_BIN_PATH);
            appParameters.serializedObject.ApplyModifiedProperties();
        }

        private static void SetCreatorHubPath(SerializedProperty appParameters, string path)
        {
            // First remove existing path if any
            RemoveFlagWithValue(appParameters, AppArgsFlags.CREATOR_HUB_BIN_PATH);

            // Add --flag and value
            int insertIndex = appParameters.arraySize;
            appParameters.InsertArrayElementAtIndex(insertIndex);
            appParameters.GetArrayElementAtIndex(insertIndex).stringValue = FormatFlag(AppArgsFlags.CREATOR_HUB_BIN_PATH);

            appParameters.InsertArrayElementAtIndex(insertIndex + 1);
            appParameters.GetArrayElementAtIndex(insertIndex + 1).stringValue = path;

            appParameters.serializedObject.ApplyModifiedProperties();
        }

        private static void RemoveFlag(SerializedProperty appParameters, string flag)
        {
            string formattedFlag = FormatFlag(flag);

            for (int i = appParameters.arraySize - 1; i >= 0; i--)
            {
                if (appParameters.GetArrayElementAtIndex(i).stringValue == formattedFlag)
                {
                    // Remove the value first (if exists)
                    if (i + 1 < appParameters.arraySize)
                    {
                        string nextValue = appParameters.GetArrayElementAtIndex(i + 1).stringValue;

                        // Only remove if it's not another flag
                        if (!nextValue.StartsWith("--"))
                            appParameters.DeleteArrayElementAtIndex(i + 1);
                    }

                    // Then remove the flag
                    appParameters.DeleteArrayElementAtIndex(i);
                    appParameters.serializedObject.ApplyModifiedProperties();
                    return;
                }
            }
        }

        private static void RemoveFlagWithValue(SerializedProperty appParameters, string flag)
        {
            string formattedFlag = FormatFlag(flag);

            for (int i = appParameters.arraySize - 1; i >= 0; i--)
            {
                if (appParameters.GetArrayElementAtIndex(i).stringValue == formattedFlag)
                {
                    // Remove the value first (if exists)
                    if (i + 1 < appParameters.arraySize)
                    {
                        string nextValue = appParameters.GetArrayElementAtIndex(i + 1).stringValue;

                        // Only remove if it's not another flag
                        if (!nextValue.StartsWith("--"))
                            appParameters.DeleteArrayElementAtIndex(i + 1);
                    }

                    // Then remove the flag
                    appParameters.DeleteArrayElementAtIndex(i);
                    appParameters.serializedObject.ApplyModifiedProperties();
                    return;
                }
            }
        }
    }
}
