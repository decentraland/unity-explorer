using UnityEditor;
using UnityEngine;

namespace Editor
{
    // GPU_INSTANCING, LIGHT_SOURCE and LANDSCAPE have Error/Exception-level DebugLog reporting
    // disabled in production settings, so any real DCL.Diagnostics.ReportHub error from those
    // systems is silently swallowed rather than printed. Temporarily re-enable it for capture
    // builds so a real underlying error (if one exists) actually surfaces in Player.log.
    public static class FixReportsLogging
    {
        private const string SETTINGS_PATH = "Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportsHandlingSettingsProduction.asset";
        private static readonly string[] Categories = { "GPU_INSTANCING", "LIGHT_SOURCE", "LANDSCAPE" };

        public static void Fix()
        {
            var settingsObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SETTINGS_PATH);

            if (settingsObj == null)
            {
                Debug.LogError($"[FixReportsLogging] Could not load {SETTINGS_PATH}");
                EditorApplication.Exit(1);
                return;
            }

            var so = new SerializedObject(settingsObj);
            SerializedProperty debugLogMatrix = so.FindProperty("debugLogMatrix");
            SerializedProperty entries = debugLogMatrix.FindPropertyRelative("entries");

            int added = 0;

            foreach (string category in Categories)
            {
                foreach (LogType severity in new[] { LogType.Error, LogType.Exception })
                {
                    bool exists = false;

                    for (var i = 0; i < entries.arraySize; i++)
                    {
                        SerializedProperty entry = entries.GetArrayElementAtIndex(i);

                        if (entry.FindPropertyRelative("Category").stringValue == category
                            && (LogType)entry.FindPropertyRelative("Severity").enumValueIndex == severity)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (exists)
                        continue;

                    int newIndex = entries.arraySize;
                    entries.InsertArrayElementAtIndex(newIndex);
                    SerializedProperty newEntry = entries.GetArrayElementAtIndex(newIndex);
                    newEntry.FindPropertyRelative("Category").stringValue = category;
                    newEntry.FindPropertyRelative("Severity").enumValueIndex = (int)severity;
                    added++;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsObj);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FixReportsLogging] Added {added} entries. Total entries now: {entries.arraySize}");
        }
    }
}
