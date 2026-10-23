using DCL.BevyMigration.Inventory;
using DCL.Diagnostics;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DCL.BevyMigration.Editor
{
    /// <summary>
    ///     Unattended Play mode sessions of the main scene for the migration's gates: injects app parameters into
    ///     the loader's debug settings in memory (never saved), enters Play mode, optionally records the shader
    ///     inventory, leaves Play mode after a deadline, and repeats with a script reload in between when asked
    ///     (gate G-A1: "Play mode survives three consecutive domain reloads"). State lives in
    ///     <see cref="SessionState" /> because entering Play mode and reloading scripts both reload the domain.
    ///     <code>
    ///     Unity -projectPath Explorer -executeMethod DCL.BevyMigration.Editor.EditorPlaySession.RunBatch
    ///           [-bmAppParams "--world bevy --bevy-test-cube --mcp"] [-bmSeconds 60] [-bmCycles 3] [-bmRecordShaders]
    ///     </code>
    ///     A deadline of 0 keeps Play mode running until it is stopped by hand.
    /// </summary>
    public static class EditorPlaySession
    {
        private const string MAIN_SCENE = "Assets/Scenes/Main.unity";
        private const string LOADER_TYPE_NAME = "MainSceneLoader";
        private const string APP_PARAMETERS_PROPERTY = "debugSettings.appParameters";
        private const string APP_PARAMS_ARG = "-bmAppParams";
        private const string SECONDS_ARG = "-bmSeconds";
        private const string CYCLES_ARG = "-bmCycles";
        private const string RECORD_SHADERS_ARG = "-bmRecordShaders";
        private const int DEFAULT_SECONDS = 60;
        private const string ACTIVE_KEY = "BevyMigration.PlaySession.Active";
        private const string DEADLINE_KEY = "BevyMigration.PlaySession.Deadline";
        private const string SECONDS_KEY = "BevyMigration.PlaySession.Seconds";
        private const string CYCLES_LEFT_KEY = "BevyMigration.PlaySession.CyclesLeft";
        private const string APP_PARAMS_KEY = "BevyMigration.PlaySession.AppParams";
        private const string RECORD_KEY = "BevyMigration.PlaySession.RecordShaders";
        private const string EXIT_KEY = "BevyMigration.PlaySession.ExitPending";
        private const char PARAM_SEPARATOR = ' ';

        private static readonly ReportData REPORT_DATA = new (ReportCategory.ALWAYS);

        public static void RunBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int seconds = int.TryParse(ArgumentAfter(args, SECONDS_ARG), out int parsedSeconds) ? parsedSeconds : DEFAULT_SECONDS;
            int cycles = int.TryParse(ArgumentAfter(args, CYCLES_ARG), out int parsedCycles) ? Mathf.Max(parsedCycles, 1) : 1;
            string appParams = ArgumentAfter(args, APP_PARAMS_ARG) ?? string.Empty;
            bool record = Array.IndexOf(args, RECORD_SHADERS_ARG) >= 0;
            Start(appParams, seconds, cycles, record);
        }

        /// <summary>
        ///     Starts a session from code; the shader inventory menu uses it with recording on.
        /// </summary>
        public static void Start(string appParams, int seconds, int cycles, bool recordShaders)
        {
            SessionState.SetString(APP_PARAMS_KEY, appParams);
            SessionState.SetInt(SECONDS_KEY, seconds);
            SessionState.SetInt(CYCLES_LEFT_KEY, cycles);
            SessionState.SetBool(RECORD_KEY, recordShaders);
            BeginCycle();
        }

        private static void BeginCycle()
        {
            int seconds = SessionState.GetInt(SECONDS_KEY, DEFAULT_SECONDS);
            string appParams = SessionState.GetString(APP_PARAMS_KEY, string.Empty);
            int cyclesLeft = SessionState.GetInt(CYCLES_LEFT_KEY, 1);

            EditorSceneManager.OpenScene(MAIN_SCENE, OpenSceneMode.Single);
            InjectAppParameters(appParams.Split(PARAM_SEPARATOR, StringSplitOptions.RemoveEmptyEntries));

            SessionState.SetFloat(DEADLINE_KEY, seconds > 0 ? (float)(EditorApplication.timeSinceStartup + seconds) : 0f);
            SessionState.SetBool(ACTIVE_KEY, true);
            ReportHub.Log(REPORT_DATA, $"Play session: {seconds}s, {cyclesLeft} cycle(s) left, app parameters [{appParams}]");
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

            if (SessionState.GetBool(RECORD_KEY, false) && !ShaderFamilyInventory.IsRecording)
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

            if (ShaderFamilyInventory.IsRecording)
                ShaderFamilyInventory.Stop();

            int cyclesLeft = SessionState.GetInt(CYCLES_LEFT_KEY, 1) - 1;
            SessionState.SetInt(CYCLES_LEFT_KEY, cyclesLeft);
            ReportHub.Log(REPORT_DATA, $"Play session cycle over, {cyclesLeft} left");
            EditorApplication.playModeStateChanged += OnBackInEditMode;
            EditorApplication.ExitPlaymode();
        }

        private static void OnBackInEditMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;

            EditorApplication.playModeStateChanged -= OnBackInEditMode;

            if (SessionState.GetInt(CYCLES_LEFT_KEY, 0) > 0)
            {
                // Next frame, so the state change that called this has finished unwinding.
                EditorApplication.delayCall += BeginCycle;
                return;
            }

            SessionState.SetBool(EXIT_KEY, true);
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
