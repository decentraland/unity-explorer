using DCL.BevyMigration.Inventory;
using DCL.Diagnostics;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DCL.BevyMigration.Editor
{
    /// <summary>
    ///     Unattended play-session recording for the shader inventory: opens the main scene, injects a guest session's
    ///     app parameters into the loader's debug settings in memory (never saved), enters Play mode, records for the
    ///     requested seconds, writes ledger/shader-families.md and exits the Editor. State lives in
    ///     <see cref="SessionState" /> because entering Play mode reloads the domain.
    ///     <code>
    ///     Unity -batchmode -projectPath Explorer -executeMethod DCL.BevyMigration.Editor.ShaderFamilyPlaySession.RecordBatch
    ///           [-shaderInventorySeconds 240] [-shaderInventoryAppParams "--position 0,0 ..."]
    ///     </code>
    ///     A duration of 0 records without a deadline: the session is driven by hand in the Editor window and the
    ///     report is written when Play mode stops or the Stop menu item is used.
    /// </summary>
    public static class ShaderFamilyPlaySession
    {
        private const string MAIN_SCENE = "Assets/Scenes/Main.unity";
        private const string LOADER_TYPE_NAME = "MainSceneLoader";
        private const string APP_PARAMETERS_PROPERTY = "debugSettings.appParameters";
        private const string SECONDS_ARG = "-shaderInventorySeconds";
        private const string APP_PARAMS_ARG = "-shaderInventoryAppParams";
        private const int DEFAULT_SECONDS = 240;
        private const string ACTIVE_KEY = "BevyMigration.ShaderInventory.Active";
        private const string DEADLINE_KEY = "BevyMigration.ShaderInventory.Deadline";
        private const string EXIT_KEY = "BevyMigration.ShaderInventory.ExitPending";

        // A guest lands straight on Genesis Plaza; the MCP server lets a shell watch the session from outside.
        private static readonly string[] DEFAULT_APP_PARAMS =
        {
            "--skip-auth-screen", "--ephemeral-guest-account", "true", "--position", "0,0",
            "--skip-version-check", "--skip-minimum-specs-screen", "--mcp",
        };

        private static readonly ReportData REPORT_DATA = new (ReportCategory.DEBUG);

        public static void RecordBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int seconds = int.TryParse(ArgumentAfter(args, SECONDS_ARG), out int parsed) ? parsed : DEFAULT_SECONDS;
            string? customParams = ArgumentAfter(args, APP_PARAMS_ARG);
            string[] appParams = customParams != null ? customParams.Split(' ', StringSplitOptions.RemoveEmptyEntries) : DEFAULT_APP_PARAMS;

            EditorSceneManager.OpenScene(MAIN_SCENE, OpenSceneMode.Single);
            InjectAppParameters(appParams);

            SessionState.SetFloat(DEADLINE_KEY, seconds > 0 ? (float)(EditorApplication.timeSinceStartup + seconds) : 0f);
            SessionState.SetBool(ACTIVE_KEY, true);
            ReportHub.Log(REPORT_DATA, $"Shader inventory play session: {seconds}s with app parameters [{string.Join(" ", appParams)}]");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (SessionState.GetBool(EXIT_KEY, false))
            {
                SessionState.EraseBool(EXIT_KEY);
                EditorApplication.Exit(0);
                return;
            }

            if (SessionState.GetBool(ACTIVE_KEY, false))
                EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!Application.isPlaying) return;

            if (!ShaderFamilyInventory.IsRecording)
                ShaderFamilyInventory.Start(ShaderFamilyReport.LedgerPath);

            float deadline = SessionState.GetFloat(DEADLINE_KEY, 0f);

            if (deadline <= 0f)
            {
                EditorApplication.update -= Tick;
                SessionState.EraseBool(ACTIVE_KEY);
                return;
            }

            if (EditorApplication.timeSinceStartup < deadline) return;

            EditorApplication.update -= Tick;
            SessionState.EraseBool(ACTIVE_KEY);
            ReportHub.Log(REPORT_DATA, $"Shader inventory play session over: {ShaderFamilyInventory.ShadersSeen} shaders seen");
            ShaderFamilyInventory.Stop();
            SessionState.SetBool(EXIT_KEY, true);
            EditorApplication.playModeStateChanged += ExitWhenBackInEditMode;
            EditorApplication.ExitPlaymode();
        }

        private static void ExitWhenBackInEditMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;

            EditorApplication.playModeStateChanged -= ExitWhenBackInEditMode;
            SessionState.EraseBool(EXIT_KEY);
            EditorApplication.Exit(0);
        }

        private static void InjectAppParameters(string[] appParams)
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour.GetType().Name != LOADER_TYPE_NAME) continue;

                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.FindProperty(APP_PARAMETERS_PROPERTY);
                property.arraySize = appParams.Length;

                for (var i = 0; i < appParams.Length; i++)
                    property.GetArrayElementAtIndex(i).stringValue = appParams[i];

                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            throw new InvalidOperationException($"{MAIN_SCENE} has no {LOADER_TYPE_NAME} to inject app parameters into");
        }

        private static string? ArgumentAfter(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == name)
                    return args[i + 1];

            return null;
        }
    }
}
