
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace DCL.Harness
{
    public static class DclPlaytestHarness
    {

        private static string REPORT_PATH = EnvOr("DCL_HARNESS_REPORT", @"C:\Users\builder\harness-report.json");
        private static string SHOTS_DIR   = EnvOr("DCL_HARNESS_SHOTS",  @"C:\Users\builder\harness-shots");

        private static readonly string ATLAS_DIR = EnvOr("DCL_ATLAS_DIR", Path.DirectorySeparatorChar == '/' ? "/tmp/dcl-atlas" : @"C:\Users\builder");
        private static string EnvOr(string envVar, string fallback)
        {
            try { var v = Environment.GetEnvironmentVariable(envVar); return string.IsNullOrEmpty(v) ? fallback : v; }
            catch { return fallback; }
        }

        public static void SetCapturePaths(string shots, string report)
        {
            if (!string.IsNullOrEmpty(shots))  SHOTS_DIR   = shots;
            if (!string.IsNullOrEmpty(report)) REPORT_PATH = report;
            Debug.Log($"[Harness] capture paths set: SHOTS_DIR={SHOTS_DIR} REPORT_PATH={REPORT_PATH}");
        }

        private static readonly System.Collections.Generic.HashSet<string> atlasOnlyOverride = new System.Collections.Generic.HashSet<string>();
        private static bool atlasOnlyOverrideSet;
        public static void SetAtlasOnly(string csv)
        {

            atlasOnlyOverrideSet = true;
            atlasOnlyOverride.Clear();
            if (!string.IsNullOrEmpty(csv))
                foreach (var tok in csv.Split(new[] { ',', ' ', '\n', '\r', '\t', '﻿' }, StringSplitOptions.RemoveEmptyEntries))
                    atlasOnlyOverride.Add(tok.Trim().Trim('﻿'));
            Debug.Log("[Harness] atlas-only override set: " + (atlasOnlyOverride.Count == 0 ? "(cleared -> FULL)" : string.Join(",", atlasOnlyOverride)));
        }

        private static int atlasSettleSeconds = (int)EnvFloatOr("DCL_ATLAS_SETTLE_SECONDS", 8f);
        private static float EnvFloatOr(string envVar, float fallback)
        {
            try
            {
                return float.TryParse(Environment.GetEnvironmentVariable(envVar),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                    out var v) && v >= 0 ? v : fallback;
            }
            catch { return fallback; }
        }
        private const float  LOAD_TIMEOUT_S   = 180f;
        private const float  SAMPLE_SECONDS   = 20f;
        private const float  SETTLE_AFTER_TP  = 8f;

        private static int captureW     = 3840;
        private static int captureH     = 2160;
        private static int captureSuper = 1;

        private static Vector2Int atlasParcel = new Vector2Int(140, 140);
        public static void SetCaptureResolution(int width, int height, int superSize)
        {
            captureW = Mathf.Max(16, width);
            captureH = Mathf.Max(16, height);
            captureSuper = Mathf.Clamp(superSize, 1, 8);
            Debug.Log($"[Harness] capture resolution set: {captureW}x{captureH} x{captureSuper} = {captureW * captureSuper}x{captureH * captureSuper}");
        }

        private static void ApplyCaptureArgsFromCommandLine()
        {
            try
            {
                foreach (string a in Environment.GetCommandLineArgs())
                {
                    if (a.StartsWith("--harness-res=", StringComparison.Ordinal))
                    {
                        string[] wh = a.Substring("--harness-res=".Length).Split('x', 'X');
                        if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h))
                        { captureW = Mathf.Max(16, w); captureH = Mathf.Max(16, h); }
                    }
                    else if (a.StartsWith("--harness-super=", StringComparison.Ordinal)
                             && int.TryParse(a.Substring("--harness-super=".Length), out int s))
                        captureSuper = Mathf.Clamp(s, 1, 8);
                    else if (a.StartsWith("--harness-atlas-parcel=", StringComparison.Ordinal))
                    {
                        string[] xy = a.Substring("--harness-atlas-parcel=".Length).Split(',');
                        if (xy.Length == 2 && int.TryParse(xy[0], out int px) && int.TryParse(xy[1], out int py))
                            atlasParcel = new Vector2Int(px, py);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Harness] capture-arg parse failed: " + e.Message); }
        }

        private static readonly Vector2Int[] TELEPORT_TARGETS =
        {
            new Vector2Int(0,  0),
            new Vector2Int(-9, -9),
            new Vector2Int(74, -9),

        };

        private static readonly List<LogEntry> warnings = new();
        private static readonly List<LogEntry> errors   = new();
        private static int totalLogCount;
        private static int shotIndex;

        private struct LogEntry { public string message; public string stack; public string type; public double t; }

        private const string KEY_RUN  = "DclHarness.Run";
        private const string KEY_EXIT = "DclHarness.ExitOnFinish";
        private const string KEY_QUIT = "DclHarness.QuitWhenEditMode";
        private const string KEY_MODE = "DclHarness.Mode";

        private static readonly string CPU_CSV_PATH    = Environment.GetEnvironmentVariable("DCL_CPU_CSV")    ?? Path.Combine(ATLAS_DIR, "harness-cpu.csv");
        private const float  CPU_SETTLE_S    = 35f;
        private const float  CPU_WARMUP_S    = 3f;
        private const float  CPU_SAMPLE_S    = 15f;
        private const int    CPU_MAX_MARKERS = 600;
        private const int    CPU_TOP_N       = 70;

        private static readonly string SHADOW_CSV_PATH = Environment.GetEnvironmentVariable("DCL_SHADOW_CSV") ?? Path.Combine(ATLAS_DIR, "harness-shadow.csv");
        private const float  SHADOW_SETTLE_S = 30f;

        private static readonly string RENDER_CSV_PATH = Environment.GetEnvironmentVariable("DCL_RENDER_CSV") ?? Path.Combine(ATLAS_DIR, "harness-render.csv");
        private const int    RENDER_WINDOWS_PER_KNOB = 24;

        private static readonly string PERF_CSV_PATH   = Environment.GetEnvironmentVariable("DCL_PERF_CSV")   ?? Path.Combine(ATLAS_DIR, "harness-perf.csv");
        private const float  PERF_WARMUP_S   = 4f;
        private const float  PERF_WINDOW_S   = 1f;
        private const int    PERF_WINDOWS    = 80;
        private const int    PERF_DROP_FRAMES = 4;
        private static bool _exitOnFinish;
        private static bool _runActive;

        public static void RunHeadless()
        {
            Debug.Log("[Harness] RunHeadless invoked; arming and entering Play mode.");
            Arm(exitOnFinish: true, mode: "session");
        }

        public static void RunPerfHeadless()
        {
            Debug.Log("[Harness] RunPerfHeadless invoked; arming perf-benchmark mode.");
            Arm(exitOnFinish: true, mode: "perf");
        }

        public static void RunCpuBreakdownHeadless()
        {
            Debug.Log("[Harness] RunCpuBreakdownHeadless invoked; arming CPU-breakdown mode.");
            Arm(exitOnFinish: true, mode: "cpu");
        }

        public static void RunShadowPerfHeadless()
        {
            Debug.Log("[Harness] RunShadowPerfHeadless invoked; arming shadow A/B mode.");
            Arm(exitOnFinish: true, mode: "shadow");
        }

        public static void RunRenderDecompHeadless()
        {
            Debug.Log("[Harness] RunRenderDecompHeadless invoked; arming render-decomposition mode.");
            Arm(exitOnFinish: true, mode: "render");
        }

        public static void RunAuthCaptureHeadless()
        {
            Debug.Log("[Harness] RunAuthCaptureHeadless invoked; arming auth-screen-capture mode.");
            Arm(exitOnFinish: true, mode: "auth");
        }

        [MenuItem("DCL/Harness/Capture Auth Screens")]
        public static void RunAuthCaptureFromMenu() => Arm(exitOnFinish: false, mode: "auth");

        public static void RunAtlasHeadless()
        {
            Debug.Log("[Harness] RunAtlasHeadless invoked; arming atlas UI-surface capture mode.");
            Arm(exitOnFinish: true, mode: "atlas");
        }

        [MenuItem("DCL/Harness/Capture Atlas (UI surface)")]
        public static void RunAtlasFromMenu() => Arm(exitOnFinish: false, mode: "atlas");

        [MenuItem("DCL/Harness/Comms Hold at Genesis Plaza (live multi-client)")]
        public static void RunCommsHoldFromMenu() => Arm(exitOnFinish: false, mode: "comms");

        [MenuItem("DCL/Harness/Capture Resolution/720p native (1280x720)")]   public static void Res720()  => SetCaptureResolution(1280, 720, 1);
        [MenuItem("DCL/Harness/Capture Resolution/1080p native (1920x1080)")] public static void Res1080() => SetCaptureResolution(1920, 1080, 1);
        [MenuItem("DCL/Harness/Capture Resolution/1440p native (2560x1440)")] public static void Res1440() => SetCaptureResolution(2560, 1440, 1);
        [MenuItem("DCL/Harness/Capture Resolution/4K native (3840x2160)")]    public static void Res4K()   => SetCaptureResolution(3840, 2160, 1);
        [MenuItem("DCL/Harness/Capture Resolution/5K native (5120x2880)")]    public static void Res5K()   => SetCaptureResolution(5120, 2880, 1);

        [MenuItem("DCL/Harness/Run Genesis Plaza Playtest")]
        public static void RunFromMenu() => Arm(exitOnFinish: false, mode: "session");

        [MenuItem("DCL/Harness/Run Perf Benchmark (Backpack preview)")]
        public static void RunPerfFromMenu() => Arm(exitOnFinish: false, mode: "perf");

        [MenuItem("DCL/Harness/Run CPU Breakdown (steady-state markers)")]
        public static void RunCpuFromMenu() => Arm(exitOnFinish: false, mode: "cpu");

        [MenuItem("DCL/Harness/Run Shadow A/B (cost of shadows)")]
        public static void RunShadowFromMenu() => Arm(exitOnFinish: false, mode: "shadow");

        [MenuItem("DCL/Harness/Run Render Decomposition (per-knob cost)")]
        public static void RunRenderFromMenu() => Arm(exitOnFinish: false, mode: "render");

        private static void Arm(bool exitOnFinish, string mode)
        {
            ApplyCaptureArgsFromCommandLine();

            try
            {
                const string mainScenePath = "Assets/Scenes/Main.unity";
                if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != mainScenePath)
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(mainScenePath);
            }
            catch (Exception e) { Debug.LogWarning("[Harness] Could not open Main.unity: " + e.Message); }

            try
            {
                var msl = FindMainSceneLoader();
                if (msl is UnityEngine.Object uo)
                {
                    var so = new SerializedObject(uo);
                    var prop = so.FindProperty("debugSettings.showAuthentication");
                    if (prop != null)
                    {
                        bool showAuth = (mode == "auth");
                        prop.boolValue = showAuth;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        Debug.Log($"[Harness] debugSettings.showAuthentication={showAuth} (mode={mode}).");
                    }
                    else Debug.LogWarning("[Harness] could not find debugSettings.showAuthentication property.");
                }
                else Debug.LogWarning("[Harness] MainSceneLoader not found in edit scene to set auth-skip.");
            }
            catch (Exception e) { Debug.LogWarning("[Harness] auth-skip set failed: " + e.Message); }

            SessionState.SetBool(KEY_RUN, true);
            SessionState.SetBool(KEY_EXIT, exitOnFinish);
            SessionState.SetBool(KEY_QUIT, false);
            SessionState.SetString(KEY_MODE, mode);

            if (EditorApplication.isPlaying) OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
            else EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(KEY_RUN, false))
            {
                if (_runActive) { Debug.LogWarning("[Harness] a run is already active — ignoring concurrent trigger (would double-count logs / race shared state)."); return; }
                _runActive = true;
                SessionState.SetBool(KEY_RUN, false);
                _exitOnFinish = SessionState.GetBool(KEY_EXIT, false);
                Application.logMessageReceivedThreaded -= OnLog;
                warnings.Clear(); errors.Clear(); totalLogCount = 0;
                Application.logMessageReceivedThreaded += OnLog;
                string mode = SessionState.GetString(KEY_MODE, "session");
                SessionState.SetString(KEY_MODE, "session");
                Debug.Log($"[Harness] Play mode entered; mode={mode}.");
                IEnumerator routine = mode == "perf"   ? RunPerfCoroutine()
                                    : mode == "cpu"    ? RunCpuBreakdownCoroutine()
                                    : mode == "shadow" ? RunShadowPerfCoroutine()
                                    : mode == "render" ? RunRenderDecompCoroutine()
                                    : mode == "auth"   ? RunAuthCaptureCoroutine()
                                    : mode == "atlas"  ? RunAtlasCoroutine()
                                    : mode == "comms"  ? RunCommsHoldCoroutine()
                                    : RunSessionCoroutine();
                HarnessRunner.Start(routine);
            }
            else if (s == PlayModeStateChange.EnteredEditMode)
            {
                _runActive = false;
                if (!SessionState.GetBool(KEY_QUIT, false)) return;
                SessionState.SetBool(KEY_QUIT, false);
                bool exit = SessionState.GetBool(KEY_EXIT, false);
                SessionState.SetBool(KEY_EXIT, false);
                Debug.Log("[Harness] Back in edit mode after run; exitEditor=" + exit);
                if (exit) EditorApplication.Exit(0);
            }
        }

        private static readonly string[] AUTH_SUBVIEWS =
        {
            "LoginSelectionAuthView", "VerificationDappAuthView", "VerificationOTPAuthView",
            "ProfileFetchingAuthView", "LobbyForExistingAccountAuthView", "LobbyForNewAccountAuthView",
        };
        private static void HideAuthSubViews(object authView)
        {
            if (authView == null) return;
            foreach (string name in AUTH_SUBVIEWS)
            {
                try
                {
                    object sub = GetMember(authView, name);
                    if (sub == null) continue;

                    var hide = sub.GetType().GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                    if (hide != null) hide.Invoke(sub, null);
                    object go = GetMember(sub, "gameObject");
                    var setActive = go?.GetType().GetMethod("SetActive", new[] { typeof(bool) });
                    setActive?.Invoke(go, new object[] { false });
                }
                catch { }
            }
        }

        private static void ActivateAuthSubView(object authView, string name)
        {
            try
            {
                object sub = GetMember(authView, name);
                object go = sub != null ? GetMember(sub, "gameObject") : null;
                var setActive = go?.GetType().GetMethod("SetActive", new[] { typeof(bool) });
                setActive?.Invoke(go, new object[] { true });
            }
            catch { }
        }

        private static IEnumerator RunAuthCaptureCoroutine()
        {
            SetGameViewSize16x9(captureW, captureH);
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("o") };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var marker = new PhaseMarker { label = "auth_capture", ok = true };
            report.actions.Add(marker);

            object authCtl = null, curStateProp = null, mvcManager = null;
            float findUntil = UnityEngine.Time.realtimeSinceStartup + 60f;
            while (authCtl == null && UnityEngine.Time.realtimeSinceStartup < findUntil)
            {
                try
                {
                    object loader = FindMainSceneLoader();
                    object dyn = loader != null ? GetPrivateField(loader, "dynamicWorldContainer") : null;
                    object mvc = dyn != null ? GetPublicProperty(dyn, "MvcManager") : null;
                    if (mvc != null) { mvcManager = mvc; authCtl = FindControllerByTypeName(mvc, "AuthenticationScreenController"); }
                    if (authCtl != null) curStateProp = GetMember(authCtl, "CurrentState");
                }
                catch { }
                if (authCtl != null) break;
                yield return null;
            }

            if (authCtl != null && mvcManager != null)
            {
                HideDebugPanel(GetPrivateField(FindMainSceneLoader(), "staticContainer"));
                object authView = null;
                float vUntil = UnityEngine.Time.realtimeSinceStartup + 25f;
                while (authView == null && UnityEngine.Time.realtimeSinceStartup < vUntil)
                {
                    authView = GetMember(authCtl, "viewInstance");
                    if (authView == null) yield return null;
                }
                if (authView != null)
                {

                    object sc = GetPrivateField(FindMainSceneLoader(), "staticContainer");

                    HideDebugPanel(sc); HideAuthSubViews(authView); ActivateAuthSubView(authView, "LoginSelectionAuthView");
                    for (int k = 0; k < 6; k++) yield return null;
                    yield return AtlasCapture_login(mvcManager, null, null, null, report);

                    HideDebugPanel(sc); HideAuthSubViews(authView); ActivateAuthSubView(authView, "VerificationDappAuthView");
                    for (int k = 0; k < 6; k++) yield return null;
                    yield return AtlasCapture_verify(mvcManager, null, null, null, report);

                    HideDebugPanel(sc); HideAuthSubViews(authView); ActivateAuthSubView(authView, "VerificationDappAuthView");
                    for (int k = 0; k < 6; k++) yield return null;
                    yield return AtlasCapture_web3confirm(mvcManager, null, null, null, report);

                    HideDebugPanel(sc); HideAuthSubViews(authView); ActivateAuthSubView(authView, "VerificationOTPAuthView");
                    for (int k = 0; k < 6; k++) yield return null;
                    yield return AtlasCapture_otp(mvcManager, null, null, null, report);

                    HideDebugPanel(sc); HideAuthSubViews(authView); ActivateAuthSubView(authView, "LobbyForNewAccountAuthView");
                    for (int k = 0; k < 6; k++) yield return null;
                    yield return AtlasCapture_lobbynew(mvcManager, null, null, null, report);
                }
            }

            var seen = new System.Collections.Generic.HashSet<string>();
            string last = null;
            float endAt = UnityEngine.Time.realtimeSinceStartup + 100f;
            float nextTimed = UnityEngine.Time.realtimeSinceStartup + 8f;
            int timedIdx = 0;
            object authSc = GetPrivateField(FindMainSceneLoader(), "staticContainer");
            while (UnityEngine.Time.realtimeSinceStartup < endAt)
            {
                HideDebugPanel(authSc);
                string st = null;
                try { object v = curStateProp != null ? GetMember(curStateProp, "Value") : null; st = v?.ToString(); } catch { }

                if (st != null && st != last)
                {
                    last = st;
                    if (seen.Add(st))
                    {
                        for (int i = 0; i < 18; i++) yield return null;
                        yield return CaptureShot("auth_" + st);

                        string mapped = st == "LoginSelectionScreen" ? "login"
                                      : st.IndexOf("ProfileFetching", StringComparison.OrdinalIgnoreCase) >= 0 ? "profilefetching"
                                      : st == "LoggedInCached" ? "lobby" : null;
                        if (mapped != null) yield return CaptureShot(mapped);
                    }
                }
                if (UnityEngine.Time.realtimeSinceStartup >= nextTimed && timedIdx < 8)
                {
                    nextTimed = UnityEngine.Time.realtimeSinceStartup + 12f;
                    yield return CaptureShot($"auth_t{(++timedIdx) * 12}s");
                }
                yield return null;
            }

            marker.error = authCtl != null
                ? "auth states seen: " + (seen.Count > 0 ? string.Join(",", seen) : "<none read>")
                : "AuthenticationScreenController not found via MVCManager (pre-in-world); timed shots only";
            report.reachedInteractive = false;
            Finish(report, sw);
        }

        private static void HideDebugPanel(object staticContainer)
        {
            try
            {
                object dcb = staticContainer != null ? GetPublicProperty(staticContainer, "DebugContainerBuilder") : null;
                if (dcb == null) return;
                var prop = dcb.GetType().GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite) prop.SetValue(dcb, false);
            }
            catch { }
        }

        public static void HideDebugPanel()
        {
            object sc = GetPrivateField(FindMainSceneLoader(), "staticContainer");
            if (sc == null) { Debug.LogWarning("[Harness] HideDebugPanel: staticContainer not found (in Play?)"); return; }
            HideDebugPanel(sc);
            Debug.Log("[Harness] debug panel hidden");
        }

        public static void HideRewardsPopup()
        {
            string[] needles = { "NewNotificationPanel", "RewardsHUD", "RewardsPopup", "MarketplaceCreditsMenu", "CreditsUnlocked" };
            int hidden = 0;
            foreach (var tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (tr == null) continue;
                var go = tr.gameObject;
                if (!go.activeSelf) continue;
                foreach (var needle in needles)
                {
                    if (go.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    go.SetActive(false); hidden++;
                    Debug.Log("[Harness] HideRewardsPopup: hid " + go.name);
                    break;
                }
            }
            Debug.Log($"[Harness] HideRewardsPopup: hid {hidden} popup root(s)");
        }

        private static void HideChat(object mvcManager)
        {
            int hidden = 0;

            try
            {
                object chatCtl = mvcManager != null ? FindControllerByTypeName(mvcManager, "ChatMainSharedAreaController") : null;
                object view = chatCtl != null ? GetMember(chatCtl, "viewInstance") : null;
                if (view is UnityEngine.MonoBehaviour mb && mb != null)
                {
                    var go = mb.gameObject;
                    if (go != null && go.activeSelf) { go.SetActive(false); hidden++; }
                }
            }
            catch { }

            if (hidden == 0)
            {
                string[] needles = { "ChatMainSharedAreaView", "ChatPanelView" };
                foreach (var tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                {
                    if (tr == null) continue;
                    var go = tr.gameObject;
                    if (!go.activeSelf) continue;
                    foreach (var needle in needles)
                    {
                        if (go.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        go.SetActive(false); hidden++;
                        break;
                    }
                }
            }
            Debug.Log($"[Harness] HideChat: hid {hidden} chat root(s)");
        }

        private static IEnumerator RunAtlasCoroutine()
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("o") };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            shotIndex = 0;
            try
            {
                if (Directory.Exists(SHOTS_DIR)) Directory.Delete(SHOTS_DIR, true);
                Directory.CreateDirectory(SHOTS_DIR);
            }
            catch (Exception e) { Debug.LogWarning("[Harness] atlas: could not reset shots dir: " + e.Message); }

            for (int i = 0; i < 3 && EditorApplication.isPlaying; i++) yield return null;

            object mainSceneLoader = null;
            float findDeadline = UnityEngine.Time.realtimeSinceStartup + 30f;
            while (mainSceneLoader == null && UnityEngine.Time.realtimeSinceStartup < findDeadline)
            {
                mainSceneLoader = FindMainSceneLoader();
                if (mainSceneLoader == null) yield return null;
            }
            if (mainSceneLoader == null)
            {
                report.fatal = "atlas: Could not find MainSceneLoader.";
                Finish(report, sw); yield break;
            }

            object loadingStatus = null;
            float ttiStart = UnityEngine.Time.realtimeSinceStartup;
            float ttiDeadline = ttiStart + LOAD_TIMEOUT_S;
            bool reachedInteractive = false;
            while (UnityEngine.Time.realtimeSinceStartup < ttiDeadline)
            {
                if (loadingStatus == null)
                {
                    var sc = GetPrivateField(mainSceneLoader, "staticContainer");
                    if (sc != null) loadingStatus = GetPublicProperty(sc, "LoadingStatus");
                }
                if (loadingStatus != null)
                {
                    string stage = ReadLoadingStage(loadingStatus);
                    report.lastLoadingStage = stage;
                    if (stage == "Completed") { reachedInteractive = true; break; }
                }
                yield return null;
            }
            report.reachedInteractive = reachedInteractive;
            report.timeToInteractiveSeconds = reachedInteractive ? UnityEngine.Time.realtimeSinceStartup - ttiStart : -1f;
            Debug.Log($"[Harness] atlas TTI={report.timeToInteractiveSeconds:F2}s reached={reachedInteractive}");

            object dynamicContainer = GetPrivateField(mainSceneLoader, "dynamicWorldContainer");
            object staticContainer2 = GetPrivateField(mainSceneLoader, "staticContainer");
            object realmNavigator   = dynamicContainer != null ? GetPublicProperty(dynamicContainer, "RealmNavigator") : null;
            object mvcManager       = dynamicContainer != null ? GetPublicProperty(dynamicContainer, "MvcManager") : null;
            report.foundRealmNavigator = realmNavigator != null;

            SetGameViewSize16x9(captureW, captureH);
            HideDebugPanel(staticContainer2);

            if (!reachedInteractive || mvcManager == null)
            {
                report.fatal = "atlas: not interactive or no MvcManager; aborting (reached=" + reachedInteractive + ", mvc=" + (mvcManager != null) + ")";
                Finish(report, sw); yield break;
            }

            int extraSettle = 0;
            try
            {
                const string parcelFile = @"C:\Users\builder\atlas-parcel.txt";
                if (File.Exists(parcelFile))
                {
                    string[] xy = File.ReadAllText(parcelFile).Trim().Trim('﻿').Split(',');
                    if (xy.Length == 2 && int.TryParse(xy[0], out int px) && int.TryParse(xy[1], out int py))
                    { atlasParcel = new Vector2Int(px, py); extraSettle = 600; }
                    File.Delete(parcelFile);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Harness] atlas-parcel read failed: " + e.Message); }

            if (realmNavigator != null)
            {
                var noiseMark = new PhaseMarker { label = "atlas_teleport_quiet", ok = true };
                report.actions.Add(noiseMark);
                bool tok = TryTeleport(realmNavigator, atlasParcel, out string terr);
                noiseMark.error = tok ? ("to " + atlasParcel.x + "," + atlasParcel.y) : ("teleport failed: " + terr);

                for (int i = 0; i < 240 + extraSettle && tok; i++) yield return null;
            }

            if (mvcManager != null) yield return CloseOpenPanels(mvcManager);
            yield return CaptureShot("atlas_world_quiet");

            var atlasDrivers = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, IEnumerator>>();

            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("explore", AtlasCapture_explore(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("settings", RunRoute(Folded("settings"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("places", AtlasCapture_places(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("events", AtlasCapture_events(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("map", RunRoute(Folded("map"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("backpack", RunRoute(Folded("backpack"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("backpackemotes", AtlasCapture_backpackemotes(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("backpackoutfits", AtlasCapture_backpackoutfits(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("placedetail", AtlasCapture_placedetail(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("eventdetail", AtlasCapture_eventdetail(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("navigation", AtlasCapture_navigation(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("iteminfo", AtlasCapture_iteminfo(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("mapfilters", AtlasCapture_mapfilters(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("sidebar", AtlasCapture_sidebar(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("minimap", AtlasCapture_minimap(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("chat", AtlasCapture_chat(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("notifications", AtlasCapture_notifications(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("voice", RunRoute(Folded("voice"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("connectionstatus", AtlasCapture_connectionstatus(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("profilewidget", RunRoute(Folded("profilewidget"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("chatprofile", AtlasCapture_chatprofile(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("marketplace", RunRoute(Folded("marketplace"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("donations", AtlasCapture_donations(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("camera", AtlasCapture_camera(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("reel", AtlasCapture_reel(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("photo", AtlasCapture_photo(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("gifting", AtlasCapture_gifting(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("creditsunlocked", AtlasCapture_creditsunlocked(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("creditsstates", AtlasCapture_creditsstates(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("emotewheel", RunRoute(Folded("emotewheel"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("skybox", RunRoute(Folded("skybox"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("teleportprompt", AtlasCapture_teleportprompt(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("nftprompt", AtlasCapture_nftprompt(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("reward", AtlasCapture_reward(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("privateworlds", AtlasCapture_privateworlds(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("smartwearables", AtlasCapture_smartwearables(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("errorpopup", AtlasCapture_errorpopup(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("controls", RunRoute(Folded("controls"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("loading", AtlasCapture_loading(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("sceneloading", AtlasCapture_sceneloading(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("minspecs", AtlasCapture_minspecs(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("updaterequired", AtlasCapture_updaterequired(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("connectionerror", AtlasCapture_connectionerror(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("friends", RunRoute(Folded("friends"), mvcManager, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("communities", AtlasCapture_communities(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("passport", AtlasCapture_passport(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("communitycreate", AtlasCapture_communitycreate(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("createcommunity", AtlasCapture_createcommunity(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("badgesdetail", AtlasCapture_badgesdetail(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("communitycard", AtlasCapture_communitycard(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("friendactions", AtlasCapture_friendactions(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("communitymembers", AtlasCapture_communitymembers(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("communitycontent", AtlasCapture_communitycontent(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("passportphotos", AtlasCapture_passportphotos(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("addlink", AtlasCapture_addlink(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("broadcast", AtlasCapture_broadcast(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("contextmenu", AtlasCapture_contextmenu(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("confirm", AtlasCapture_confirm(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("gallery", AtlasCapture_gallery(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));

            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("inputsuggestions", AtlasCapture_inputsuggestions(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("chatwindow", AtlasCapture_chatwindow(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("reactions", AtlasCapture_reactions(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("emoji", AtlasCapture_emoji(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));

            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("communitystream", AtlasCapture_communitystream(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));
            atlasDrivers.Add(new System.Collections.Generic.KeyValuePair<string, IEnumerator>("duplicateidentity", AtlasCapture_duplicateidentity(mvcManager, staticContainer2, dynamicContainer, realmNavigator, report)));

            var atlasOnly = new System.Collections.Generic.HashSet<string>();
            try
            {
                string onlyFile = Environment.GetEnvironmentVariable("DCL_ATLAS_ONLY") ?? Path.Combine(ATLAS_DIR, "atlas-only.txt");
                if (File.Exists(onlyFile))
                {
                    foreach (var tok in File.ReadAllText(onlyFile).Split(new[] { ',', ' ', '\n', '\r', '\t', '﻿' }, StringSplitOptions.RemoveEmptyEntries))
                        atlasOnly.Add(tok.Trim().Trim('﻿'));
                    File.Delete(onlyFile);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Harness] atlas-only read failed: " + e.Message); }

            try
            {
                var envOnly = Environment.GetEnvironmentVariable("DCL_ATLAS_ONLY");
                if (!string.IsNullOrEmpty(envOnly))
                    foreach (var tok in envOnly.Split(new[] { ',', ' ', '\n', '\r', '\t', '﻿' }, StringSplitOptions.RemoveEmptyEntries))
                        atlasOnly.Add(tok.Trim().Trim('﻿'));
            }
            catch (Exception e) { Debug.LogWarning("[Harness] DCL_ATLAS_ONLY read failed: " + e.Message); }

            if (atlasOnlyOverrideSet) { atlasOnly.Clear(); foreach (var k in atlasOnlyOverride) atlasOnly.Add(k); }

            Debug.Log($"[Harness] atlas: {atlasDrivers.Count} drivers queued"
                      + (atlasOnly.Count > 0 ? "; SUBSET -> " + string.Join(",", atlasOnly) : " (full)"));

            var mutatingDrivers = new System.Collections.Generic.HashSet<string> { "createcommunity", "broadcast" };
            foreach (var kv in atlasDrivers)
            {
                if (atlasOnly.Count > 0 && !atlasOnly.Contains(kv.Key)) continue;
                if (mutatingDrivers.Contains(kv.Key) && !atlasOnly.Contains(kv.Key)) continue;
                Debug.Log("[Harness] atlas driver -> " + kv.Key);
                yield return kv.Value;
                yield return CloseOpenPanels(mvcManager);
                for (int i = 0; i < 8; i++) yield return null;
            }

            int shown = 0, other = 0;
            foreach (var a in report.actions)
            {
                if (!a.label.StartsWith("atlas_")) continue;

                bool good = a.ok || a.error == "shown" || (a.error != null && a.error.StartsWith("skipped:"));
                if (good) shown++; else other++;
            }
            Debug.Log($"[Harness] atlas done: shown/ok={shown} other={other}");
            Finish(report, sw);
        }

        private static IEnumerator RunSessionCoroutine()
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("o") };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            shotIndex = 0;
            try
            {
                if (Directory.Exists(SHOTS_DIR)) Directory.Delete(SHOTS_DIR, true);
                Directory.CreateDirectory(SHOTS_DIR);
            }
            catch (Exception e) { Debug.LogWarning("[Harness] could not reset shots dir: " + e.Message); }

            for (int i = 0; i < 3 && EditorApplication.isPlaying; i++) yield return null;

            object mainSceneLoader = null;
            float findDeadline = UnityEngine.Time.realtimeSinceStartup + 30f;
            while (mainSceneLoader == null && UnityEngine.Time.realtimeSinceStartup < findDeadline)
            {
                mainSceneLoader = FindMainSceneLoader();
                if (mainSceneLoader == null) yield return null;
            }
            if (mainSceneLoader == null)
            {
                report.fatal = "Could not find MainSceneLoader MonoBehaviour in the scene.";
                Finish(report, sw); yield break;
            }

            object loadingStatus = null;
            float ttiStart = UnityEngine.Time.realtimeSinceStartup;
            float ttiDeadline = ttiStart + LOAD_TIMEOUT_S;
            bool reachedInteractive = false;

            while (UnityEngine.Time.realtimeSinceStartup < ttiDeadline)
            {
                if (loadingStatus == null)
                {
                    var staticContainer = GetPrivateField(mainSceneLoader, "staticContainer");
                    if (staticContainer != null)
                        loadingStatus = GetPublicProperty(staticContainer, "LoadingStatus");
                }

                if (loadingStatus != null)
                {
                    string stage = ReadLoadingStage(loadingStatus);
                    report.lastLoadingStage = stage;
                    if (stage == "Completed") { reachedInteractive = true; break; }
                }
                yield return null;
            }
            report.timeToInteractiveSeconds = reachedInteractive
                ? UnityEngine.Time.realtimeSinceStartup - ttiStart
                : -1f;
            report.reachedInteractive = reachedInteractive;
            Debug.Log($"[Harness] TTI = {report.timeToInteractiveSeconds:F2}s reached={reachedInteractive}");

            object dynamicContainer = GetPrivateField(mainSceneLoader, "dynamicWorldContainer");
            object staticContainer2 = GetPrivateField(mainSceneLoader, "staticContainer");
            object realmNavigator   = dynamicContainer != null ? GetPublicProperty(dynamicContainer, "RealmNavigator") : null;
            object chatBus          = ReachChatBus(dynamicContainer);
            object profiler         = staticContainer2 != null ? GetPublicProperty(staticContainer2, "Profiler") : null;

            report.foundRealmNavigator = realmNavigator != null;
            report.foundChatBus        = chatBus != null;
            report.foundProfiler       = profiler != null;
            SchemaCheck(mainSceneLoader, staticContainer2, dynamicContainer);

            SetGameViewSize16x9(captureW, captureH);
            yield return SamplePhase("spawn", report, SAMPLE_SECONDS);

            if (reachedInteractive) yield return CaptureShot("world_genesis");

            if (reachedInteractive)
                yield return CheckAvatars("avatar_main", report, previewOnly: false);

            if (reachedInteractive && realmNavigator != null)
            {
                foreach (var parcel in TELEPORT_TARGETS)
                {
                    bool ok = TryTeleport(realmNavigator, parcel, out string tperr);
                    var pe = new PhaseMarker { label = $"teleport_{parcel.x}_{parcel.y}", ok = ok, error = tperr };
                    report.actions.Add(pe);

                    float until = UnityEngine.Time.realtimeSinceStartup + SETTLE_AFTER_TP;
                    while (UnityEngine.Time.realtimeSinceStartup < until) yield return null;
                    yield return SamplePhase($"after_tp_{parcel.x}_{parcel.y}", report, SAMPLE_SECONDS);

                    if (ok && (parcel.x != 0 || parcel.y != 0))
                    {
                        var ppm = new PhaseMarker { label = $"playerpos_{parcel.x}_{parcel.y}", ok = true };
                        if (!TryGetPlayerParcel(staticContainer2, out Vector2Int at, out string pperr))
                            ppm.error = "expand(non-gating): pos-unreadable: " + pperr;
                        else if (at == Vector2Int.zero)
                        {
                            ppm.ok = false;
                            ppm.error = $"STUCK-AT-0,0 after teleport (expected ~{parcel.x},{parcel.y}) — 0,0 avatar-flash regression";
                        }
                        else
                        {
                            int dx = Mathf.Abs(at.x - parcel.x), dy = Mathf.Abs(at.y - parcel.y);
                            ppm.error = (dx <= 1 && dy <= 1)
                                ? $"expand(non-gating): at {at.x},{at.y} OK"
                                : $"expand(non-gating): at {at.x},{at.y} (expected ~{parcel.x},{parcel.y}; off {dx},{dy})";
                        }
                        report.actions.Add(ppm);
                    }
                }
            }

            if (reachedInteractive && realmNavigator != null)
            {
                var gm = new PhaseMarker { label = "game_144_-7", ok = true };
                report.actions.Add(gm);
                bool gok = TryTeleport(realmNavigator, new Vector2Int(144, -7), out string gerr);
                float gu = UnityEngine.Time.realtimeSinceStartup + 12f;
                while (UnityEngine.Time.realtimeSinceStartup < gu) yield return null;
                yield return CaptureShot("world_game_antrom_144_-7");
                if (!gok) gm.error = "expand(non-gating): teleport-failed: " + gerr;
                else if (!TryGetSceneLoaded(staticContainer2, out string sname, out bool ready, out string serr))
                    gm.error = "expand(non-gating): scene-unreadable: " + serr;
                else gm.error = $"expand(non-gating): scene='{sname}' ready={ready}";
            }

            if (chatBus != null)
            {
                bool ok = TrySendChat(chatBus, "Harness automated session check " +
                                              DateTime.UtcNow.ToString("HH:mm:ss"), out string cerr);
                report.actions.Add(new PhaseMarker { label = "chat_send", ok = ok, error = cerr });

                bool ok2 = TrySendChat(chatBus, "/goto 0,0", out string cerr2);
                report.actions.Add(new PhaseMarker { label = "chat_goto", ok = ok2, error = cerr2 });
                yield return SamplePhase("after_chat", report, 5f);
            }

            object mvcManager = dynamicContainer != null ? GetPublicProperty(dynamicContainer, "MvcManager") : null;

            if (reachedInteractive && mvcManager != null)
            {

                string[] uiSections = { "Backpack", "Navmap", "CameraReel", "Communities", "Places", "Events" };

                foreach (string section in uiSections)
                {
                    yield return CloseOpenPanels(mvcManager);
                    bool ok = TryOpenExplorePanel(mvcManager, section, null, out string uierr);
                    var marker = new PhaseMarker { label = "open_" + section.ToLowerInvariant(), ok = ok, error = uierr };
                    report.actions.Add(marker);
                    yield return SamplePhase("panel_" + section.ToLowerInvariant(), report, 10f);
                    if (ok && !VerifyShown(mvcManager, lastPanelKey, out string rerr))
                    { marker.ok = false; marker.error = (marker.error == null ? "" : marker.error + "; ") + "render: " + rerr; }

                    if (section == "Backpack")
                        yield return CheckAvatars("avatar_preview", report, previewOnly: true);
                }

                string[] settingsTabs = { "GENERAL", "GRAPHICS", "SOUND", "CONTROLS", "CHAT" };

                foreach (string tab in settingsTabs)
                {
                    yield return CloseOpenPanels(mvcManager);
                    bool ok = TryOpenExplorePanel(mvcManager, "Settings", tab, out string uierr);
                    var marker = new PhaseMarker { label = "settings_" + tab.ToLowerInvariant(), ok = ok, error = uierr };
                    report.actions.Add(marker);
                    yield return SamplePhase("settings_" + tab.ToLowerInvariant(), report, 6f);
                    if (ok && !VerifyShown(mvcManager, lastPanelKey, out string rerr))
                    { marker.ok = false; marker.error = (marker.error == null ? "" : marker.error + "; ") + "render: " + rerr; }
                }

                string[] friendTabs = { "FRIENDS", "REQUESTS" };

                foreach (string tab in friendTabs)
                {
                    yield return CloseOpenPanels(mvcManager);
                    bool ok = TryOpenFriendsPanel(mvcManager, tab, out string ferr);
                    var marker = new PhaseMarker { label = "friends_" + tab.ToLowerInvariant(), ok = ok, error = ferr };
                    report.actions.Add(marker);
                    yield return SamplePhase("friends_" + tab.ToLowerInvariant(), report, 8f);
                    if (ok && !VerifyShown(mvcManager, lastPanelKey, out string rerr))
                    {

                        marker.error = (marker.error == null ? "" : marker.error + "; ") + "env-limited render: " + rerr;
                    }
                }

                string[][] extraPanels =
                {
                    new[] { "notifications", "DCL.Notifications.NotificationsMenu.NotificationsPanelController", null },
                    new[] { "marketplacecredits", "DCL.MarketplaceCredits.MarketplaceCreditsMenuController", "DCL.MarketplaceCredits.MarketplaceCreditsMenuController+Params" },

                    new[] { "explore", "DCL.ExplorePanel.ExplorePanelController", "DCL.ExplorePanel.ExplorePanelParameter" },
                    new[] { "profilewidget", "DCL.UI.Profiles.ProfileMenuController", null },
                    new[] { "emotewheel", "DCL.EmotesWheel.EmotesWheelController", null },
                    new[] { "skybox", "DCL.UI.Skybox.SkyboxMenuController", null },
                };
                foreach (string[] p in extraPanels)
                {
                    yield return CloseOpenPanels(mvcManager);
                    object xparam = null;
                    string xerr = null;
                    if (p[2] != null)
                    {
                        Type pt = FindType(p[2]);
                        if (pt == null) xerr = "param type not found: " + p[2];
                        else { try { xparam = Activator.CreateInstance(pt); } catch (Exception pe) { xerr = "param ctor failed: " + pe.Message; } }
                    }
                    bool opened = xerr == null && TryShowPanelByName(mvcManager, p[1], xparam, out xerr);
                    var marker = new PhaseMarker { label = "panel_" + p[0], ok = true };
                    report.actions.Add(marker);
                    yield return SamplePhase("panel_" + p[0], report, 6f);
                    if (!opened) marker.error = "expand(non-gating): open-failed: " + xerr;
                    else if (!VerifyShown(mvcManager, lastPanelKey, out string rerr)) marker.error = "expand(non-gating): not-shown: " + rerr;
                    else marker.error = "expand(non-gating): shown OK";
                }

                yield return EnumerateContent(mvcManager, report);

                yield return SwitchToWorldAndShoot(mvcManager, staticContainer2, realmNavigator, report);

            }

            Finish(report, sw);
        }

        private static IEnumerator SamplePhase(string label, Report report, float seconds)
        {

            var mainThread = new ProfilerRecorder(ProfilerCategory.Internal, "Main Thread", 1024);
            var gpuFrame   = new ProfilerRecorder(ProfilerCategory.Render,   "GPU Frame Time", 1024);
            var gcAlloc    = new ProfilerRecorder(ProfilerCategory.Memory,   "GC Allocated In Frame", 1024);
            var sysMem     = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");

            var drawCalls  = new ProfilerRecorder(ProfilerCategory.Render, "Draw Calls Count", 1024);
            var batches    = new ProfilerRecorder(ProfilerCategory.Render, "Batches Count", 1024);
            var setPass    = new ProfilerRecorder(ProfilerCategory.Render, "SetPass Calls Count", 1024);
            var tris       = new ProfilerRecorder(ProfilerCategory.Render, "Triangles Count", 1024);

            mainThread.Start(); gpuFrame.Start(); gcAlloc.Start();
            drawCalls.Start(); batches.Start(); setPass.Start(); tris.Start();

            var cpuMs = new List<double>();
            var gpuMs = new List<double>();
            double gcSum = 0;
            long hiccups = 0;
            int frames = 0;
            float end = UnityEngine.Time.realtimeSinceStartup + seconds;

            while (UnityEngine.Time.realtimeSinceStartup < end && EditorApplication.isPlaying)
            {
                if (mainThread.Valid && mainThread.LastValue > 0)
                {
                    double ms = mainThread.LastValue * 1e-6;
                    cpuMs.Add(ms);
                    if (mainThread.LastValue > 50_000_000) hiccups++;
                }
                if (gpuFrame.Valid && gpuFrame.LastValue > 0) gpuMs.Add(gpuFrame.LastValue * 1e-6);
                if (gcAlloc.Valid) gcSum += gcAlloc.LastValue;
                frames++;
                yield return null;
            }

            if (cpuMs.Count == 0) Debug.LogError($"[Harness] phase '{label}': 0 CPU samples — 'Main Thread' counter unavailable; cpu/fps are MISSING, not zero.");
            if (gpuMs.Count == 0) Debug.LogWarning($"[Harness] phase '{label}': 0 GPU samples — 'GPU Frame Time' unavailable (common in-Editor); gpu is MISSING, not zero.");

            var phase = new PhaseMetrics
            {
                label              = label,
                frames             = frames,
                durationSeconds    = seconds,
                cpuMsAvg           = Avg(cpuMs),
                cpuMsP99Worst      = PercentWorst(cpuMs, 0.01),
                cpuMsMax           = cpuMs.Count > 0 ? cpuMs.Max() : 0,
                fpsAvg             = cpuMs.Count > 0 ? 1000.0 / Avg(cpuMs) : 0,
                gpuMsAvg           = Avg(gpuMs),
                gpuMsMax           = gpuMs.Count > 0 ? gpuMs.Max() : 0,
                hiccupFramesOver50ms = hiccups,
                gcAllocBytesTotal  = gcSum,
                systemUsedMemoryMB = sysMem.Valid ? sysMem.LastValue / (1024.0 * 1024.0) : 0,
                drawCallsLast      = drawCalls.Valid ? drawCalls.LastValue : -1,
                batchesLast        = batches.Valid ? batches.LastValue : -1,
                setPassLast        = setPass.Valid ? setPass.LastValue : -1,
                trianglesLast      = tris.Valid ? tris.LastValue : -1,
            };
            report.phases.Add(phase);
            Debug.Log($"[Harness] phase '{label}': fps~{phase.fpsAvg:F1} cpuAvg={phase.cpuMsAvg:F2}ms hiccups={hiccups} draws={phase.drawCallsLast}");

            mainThread.Dispose(); gpuFrame.Dispose(); gcAlloc.Dispose(); sysMem.Dispose();
            drawCalls.Dispose(); batches.Dispose(); setPass.Dispose(); tris.Dispose();

            string baseLabel = label.StartsWith("after_") ? label.Substring("after_".Length) : label;
            if (baseLabel.StartsWith("panel_") || baseLabel.StartsWith("settings_") || baseLabel.StartsWith("friends_"))
                yield return CaptureShot("after_" + baseLabel);
        }

        private static object ReachChatBus(object dynamicContainer)
        {
            if (dynamicContainer == null) return null;
            object chatContainer = GetPrivateField(dynamicContainer, "chatContainer");
            object bus = chatContainer != null ? GetMember(chatContainer, "ChatMessagesBus") : null;
            if (bus == null) bus = GetMember(dynamicContainer, "chatMessagesBus");
            return bus;
        }

        private static void SchemaCheck(object loader, object staticContainer, object dynamicContainer)
        {
            var missing = new List<string>();
            void Req(bool ok, string what) { if (!ok) missing.Add(what); }

            Req(loader != null, "MainSceneLoader");
            Req(staticContainer != null, "MainSceneLoader.staticContainer");
            Req(dynamicContainer != null, "MainSceneLoader.dynamicWorldContainer");
            object mvc = dynamicContainer != null ? GetPublicProperty(dynamicContainer, "MvcManager") : null;
            object nav = dynamicContainer != null ? GetPublicProperty(dynamicContainer, "RealmNavigator") : null;
            object ls  = staticContainer  != null ? GetPublicProperty(staticContainer, "LoadingStatus") : null;
            Req(mvc != null, "DynamicWorldContainer.MvcManager");
            Req(nav != null, "DynamicWorldContainer.RealmNavigator");
            Req(ls  != null, "StaticContainer.LoadingStatus");
            if (nav != null)
                Req(nav.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m => m.Name == "TeleportToParcelAsync"), "IRealmNavigator.TeleportToParcelAsync");
            Req(ReachChatBus(dynamicContainer) != null, "DynamicWorldContainer.chatContainer.ChatMessagesBus");
            Req(FindType("DCL.ExplorePanel.ExplorePanelController") != null, "DCL.ExplorePanel.ExplorePanelController");
            Req(FindType("DCL.UI.ExploreSections") != null, "DCL.UI.ExploreSections");
            Req(FindType("DCL.Chat.History.ChatChannel") != null, "DCL.Chat.History.ChatChannel");

            if (missing.Count == 0) Debug.Log("[Harness] schema check OK — all reflected members resolved.");
            else Debug.LogError("[Harness] SCHEMA DRIFT — reflected members that no longer resolve (client likely renamed them): " + string.Join(", ", missing));
        }

        private static bool TryTeleport(object realmNavigator, Vector2Int parcel, out string err)
        {
            err = null;
            try
            {

                MethodInfo mi = null;
                foreach (var cand in realmNavigator.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (cand.Name != "TeleportToParcelAsync") continue;
                    var ps0 = cand.GetParameters();
                    if (ps0.Length >= 1 && ps0[0].ParameterType == typeof(Vector2Int)) { mi = cand; break; }
                }
                if (mi == null) { err = "TeleportToParcelAsync(Vector2Int,...) not found"; return false; }

                var ps = mi.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt == typeof(Vector2Int)) args[i] = parcel;
                    else if (pt == typeof(System.Threading.CancellationToken)) args[i] = default(System.Threading.CancellationToken);
                    else if (pt == typeof(bool)) args[i] = false;
                    else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                    else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt);
                    else args[i] = null;
                }
                mi.Invoke(realmNavigator, args);
                return true;
            }
            catch (Exception e) { err = e.InnerException?.Message ?? e.Message; return false; }
        }

        private static bool TryGetPlayerParcel(object staticContainer, out Vector2Int parcel, out string err)
        {
            parcel = Vector2Int.zero;
            err = null;
            try
            {
                if (staticContainer == null) { err = "staticContainer null"; return false; }

                object charContainer = GetMember(staticContainer, "CharacterContainer");
                if (charContainer == null) { err = "CharacterContainer not found"; return false; }

                object posObj = null;

                object charObject = GetMember(charContainer, "CharacterObject");
                if (charObject != null) posObj = GetMember(charObject, "Position");

                if (posObj == null)
                {
                    object exposed = GetMember(charContainer, "Transform");
                    object canBeDirty = exposed != null ? GetMember(exposed, "Position") : null;
                    if (canBeDirty != null) posObj = GetMember(canBeDirty, "Value");
                }
                if (posObj == null || !(posObj is Vector3)) { err = "position unreadable"; return false; }

                var p = (Vector3)posObj;
                parcel = new Vector2Int(Mathf.FloorToInt(p.x / 16f), Mathf.FloorToInt(p.z / 16f));
                return true;
            }
            catch (Exception e) { err = e.InnerException?.Message ?? e.Message; return false; }
        }

        private static bool TrySendChat(object chatBus, string message, out string err)
        {
            err = null;
            try
            {

                Type channelType = FindType("DCL.Chat.History.ChatChannel");
                Type originType  = FindType("DCL.Chat.MessageBus.ChatMessageOrigin");
                if (channelType == null || originType == null) { err = "Chat types not found"; return false; }

                object nearby = channelType.GetField("NEARBY_CHANNEL",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                object originChat = Enum.Parse(originType, "CHAT");
                double ts = DateTime.UtcNow.ToOADate();

                var send = chatBus.GetType().GetMethod("Send",
                    BindingFlags.Public | BindingFlags.Instance);
                if (send == null) { err = "IChatMessagesBus.Send not found"; return false; }
                send.Invoke(chatBus, new object[] { nearby, message, originChat, ts });
                return true;
            }
            catch (Exception e) { err = e.InnerException?.Message ?? e.Message; return false; }
        }

        private static bool TryOpenExplorePanel(object mvcManager, string sectionName, string settingsTab, out string err)
        {
            err = null;
            try
            {
                Type controllerT = FindType("DCL.ExplorePanel.ExplorePanelController");
                Type paramT      = FindType("DCL.ExplorePanel.ExplorePanelParameter");
                Type sectionsT   = FindType("DCL.UI.ExploreSections");
                if (controllerT == null || paramT == null || sectionsT == null) { err = "ExplorePanel types not found"; return false; }

                object section = Enum.Parse(sectionsT, sectionName);

                ConstructorInfo ctor = paramT.GetConstructors()[0];
                object[] ctorArgs = new object[ctor.GetParameters().Length];
                ctorArgs[0] = section;

                if (settingsTab != null)
                {
                    Type settingsSectionT = FindType("DCL.Settings.SettingsController+SettingsSection");
                    if (settingsSectionT == null) { err = "SettingsSection enum not found"; return false; }
                    if (ctorArgs.Length > 2) ctorArgs[2] = Enum.Parse(settingsSectionT, settingsTab);
                }

                object param = ctor.Invoke(ctorArgs);
                return TryShowPanel(mvcManager, controllerT, param, out err);
            }
            catch (Exception e) { err = e.Message; return false; }
        }

        private static bool TryOpenFriendsPanel(object mvcManager, string tabName, out string err)
        {
            err = null;
            try
            {
                Type controllerT = FindType("DCL.Friends.UI.FriendPanel.FriendsPanelController");
                Type paramT      = FindType("DCL.Friends.UI.FriendPanel.FriendsPanelParameter");
                Type tabT        = FindType("DCL.Friends.UI.FriendPanel.FriendsPanelController+FriendsPanelTab");
                if (controllerT == null || paramT == null || tabT == null) { err = "Friends panel types not found"; return false; }

                object tab = Enum.Parse(tabT, tabName);
                object param = Activator.CreateInstance(paramT, tab);
                return TryShowPanel(mvcManager, controllerT, param, out err);
            }
            catch (Exception e) { err = e.Message; return false; }
        }

        private static bool TryShowPanel(object mvcManager, Type controllerT, object param, out string err)
        {
            err = null;

            MethodInfo issue = controllerT.GetMethod("IssueCommand", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (issue == null) { err = "IssueCommand not found on " + controllerT.Name; return false; }
            object command = issue.Invoke(null, new[] { param });

            Type cmdType = command.GetType();
            MethodInfo showAsync = null;
            foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
            if (showAsync == null) { err = "ShowAsync not found on " + mvcManager.GetType().Name; return false; }

            Type[] genArgs = cmdType.GetGenericArguments();
            showAsync.MakeGenericMethod(genArgs)
                     .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });

            Type ifaceOpen = FindType("MVC.IController`2");
            lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
            return true;
        }

        private static Type lastPanelKey;

        private static string openDirectErr;

        private static IEnumerator OpenExplorePanelDirectCo(object mvcManager, string exploreSection, string backpackSubOrNull, string settingsTabOrNull)
        {
            openDirectErr = null;
            if (mvcManager == null) { openDirectErr = "mvcManager null"; yield break; }

            try
            {
                MethodInfo closeAll = mvcManager.GetType().GetMethod("CloseAllNonPersistentViews", BindingFlags.Public | BindingFlags.Instance);
                closeAll?.Invoke(mvcManager, new object[] { System.Threading.CancellationToken.None });
            }
            catch (Exception ce) { Debug.LogWarning("[HARNESS] OpenExplorePanelDirectCo pre-close failed: " + ce.Message); }

            for (int i = 0; i < 30; i++)
            {
                bool hidden = false;
                try
                {
                    object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                    string state = explore != null ? (GetPublicProperty(explore, "State")?.ToString()) : null;

                    hidden = explore == null || state == null || state == "ViewHidden";
                }
                catch (Exception) { hidden = false; }
                if (hidden) break;
                yield return null;
            }

            if (!OpenExplorePanelDirect(mvcManager, exploreSection, backpackSubOrNull, settingsTabOrNull, out string issueErr))
                openDirectErr = issueErr;
        }

        private static bool OpenExplorePanelDirect(object mvcManager, string exploreSection, string backpackSubOrNull, string settingsTabOrNull, out string err)
        {
            err = null;
            try
            {
                if (mvcManager == null) { err = "mvcManager null"; return false; }

                Type paramType         = FindType("DCL.ExplorePanel.ExplorePanelParameter");
                Type exploreSectionsT  = FindType("DCL.UI.ExploreSections");
                Type controllerType    = FindType("DCL.ExplorePanel.ExplorePanelController");
                if (paramType == null || exploreSectionsT == null || controllerType == null)
                { err = "explore-panel types not found (ExplorePanelParameter/ExploreSections/ExplorePanelController)"; return false; }

                object section = Enum.Parse(exploreSectionsT, exploreSection);

                ConstructorInfo ctor = paramType.GetConstructors()[0];
                object[] ctorArgs = new object[ctor.GetParameters().Length];
                ctorArgs[0] = section;
                if (backpackSubOrNull != null && ctorArgs.Length > 1)
                {
                    Type backpackSectionsT = FindType("DCL.UI.BackpackSections");
                    if (backpackSectionsT == null) { err = "BackpackSections enum not found"; return false; }
                    ctorArgs[1] = Enum.Parse(backpackSectionsT, backpackSubOrNull);
                }
                if (settingsTabOrNull != null && ctorArgs.Length > 2)
                {
                    Type settingsSectionT = FindType("DCL.Settings.SettingsController+SettingsSection");
                    if (settingsSectionT == null) { err = "SettingsSection enum not found"; return false; }
                    ctorArgs[2] = Enum.Parse(settingsSectionT, settingsTabOrNull);
                }
                object param = ctor.Invoke(ctorArgs);

                MethodInfo issueCommand = controllerType.GetMethod("IssueCommand", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (issueCommand == null) { err = "IssueCommand not found on ExplorePanelController"; return false; }
                object command = issueCommand.Invoke(null, new[] { param });
                if (command == null) { err = "IssueCommand returned null"; return false; }

                Type[] genArgs = command.GetType().GetGenericArguments();
                if (genArgs.Length != 2) { err = "command generic args count != 2"; return false; }

                MethodInfo showAsync = null;
                foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                if (showAsync == null) { err = "ShowAsync not found on MvcManager"; return false; }

                showAsync.MakeGenericMethod(genArgs)
                         .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });

                Type ifaceOpen = FindType("MVC.IController`2");
                lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                return true;
            }
            catch (Exception e) { err = e.InnerException?.Message ?? e.Message; return false; }
        }

        private static bool TryShowPanelByName(object mvcManager, string controllerFullName, object paramOrNull, out string err)
        {
            err = null;
            try
            {
                Type controllerT = FindType(controllerFullName);
                if (controllerT == null) { err = "type not found: " + controllerFullName; return false; }

                MethodInfo issue = null;
                foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    if (mi.Name == "IssueCommand")
                    {
                        int np = mi.GetParameters().Length;
                        if ((paramOrNull == null && np == 0) || (paramOrNull != null && np == 1)) { issue = mi; break; }
                        if (issue == null) issue = mi;
                    }
                if (issue == null) { err = "IssueCommand not found on " + controllerT.Name; return false; }

                object command = issue.GetParameters().Length == 0
                    ? issue.Invoke(null, null)
                    : issue.Invoke(null, new[] { paramOrNull });
                if (command == null) { err = "IssueCommand returned null"; return false; }

                MethodInfo showAsync = null;
                foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                if (showAsync == null) { err = "ShowAsync not found"; return false; }

                Type[] genArgs = command.GetType().GetGenericArguments();
                showAsync.MakeGenericMethod(genArgs)
                         .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                Type ifaceOpen = FindType("MVC.IController`2");
                lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                return true;
            }
            catch (Exception e) { err = e.Message; return false; }
        }

        private static IEnumerator CloseOpenPanels(object mvcManager)
        {
            if (mvcManager != null)
            {
                try
                {
                    MethodInfo m = mvcManager.GetType().GetMethod("CloseAllNonPersistentViews", BindingFlags.Public | BindingFlags.Instance);
                    m?.Invoke(mvcManager, new object[] { System.Threading.CancellationToken.None });
                }
                catch (Exception e) { Debug.LogWarning("[HARNESS] CloseOpenPanels failed: " + e.Message); }
            }

            for (int i = 0; i < 60; i++)
            {
                bool hidden = false;
                try
                {
                    object explore = mvcManager != null ? FindControllerByTypeName(mvcManager, "ExplorePanelController") : null;
                    string state = explore != null ? (GetPublicProperty(explore, "State")?.ToString()) : null;
                    hidden = explore == null || state == null || state == "ViewHidden";
                }
                catch (Exception) { hidden = false; }
                if (hidden) break;
                yield return null;
            }
            for (int i = 0; i < 8; i++) yield return null;
        }

        private static IEnumerator PreShowSettle(object mvcManager, string controllerTypeName)
        {
            if (mvcManager == null) yield break;
            try
            {
                MethodInfo closeAll = mvcManager.GetType().GetMethod("CloseAllNonPersistentViews", BindingFlags.Public | BindingFlags.Instance);
                closeAll?.Invoke(mvcManager, new object[] { System.Threading.CancellationToken.None });
            }
            catch (Exception e) { Debug.LogWarning("[HARNESS] PreShowSettle pre-close failed: " + e.Message); }

            for (int i = 0; i < 30; i++)
            {
                bool hidden = false;
                try
                {
                    object ctl = FindControllerByTypeName(mvcManager, controllerTypeName);
                    string state = ctl != null ? (GetPublicProperty(ctl, "State")?.ToString()) : null;
                    hidden = ctl == null || state == null || state == "ViewHidden";
                }
                catch (Exception) { hidden = false; }
                if (hidden) break;
                yield return null;
            }
        }

        private static IEnumerator HideExplorePanel(object mvcManager)
        {
            if (mvcManager != null)
            {
                try
                {
                    object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                    object view = explore != null ? GetMember(explore, "viewInstance") : null;
                    object go = view != null ? GetMember(view, "gameObject") : null;
                    var setActive = go?.GetType().GetMethod("SetActive", new[] { typeof(bool) });
                    setActive?.Invoke(go, new object[] { false });
                }
                catch (Exception e) { Debug.LogWarning("[HARNESS] HideExplorePanel failed: " + e.Message); }
            }
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static void RaiseChatBus(object mvcManager, string raiseMethod)
        {
            try
            {
                object chatCtl = mvcManager != null ? FindControllerByTypeName(mvcManager, "ChatMainSharedAreaController") : null;
                object bus = chatCtl != null ? GetPrivateField(chatCtl, "chatSharedAreaEventBus") : null;
                if (bus == null) { Debug.LogWarning("[HARNESS] RaiseChatBus: chatSharedAreaEventBus not found (" + raiseMethod + ")"); return; }
                bus.GetType().GetMethod(raiseMethod, BindingFlags.Public | BindingFlags.Instance)?.Invoke(bus, null);
            }
            catch (Exception e) { Debug.LogWarning("[HARNESS] RaiseChatBus " + raiseMethod + " failed: " + (e.InnerException?.Message ?? e.Message)); }
        }

        private static IEnumerator ShowChatDefault(object mvcManager)
        {

            try
            {
                object chatCtl = mvcManager != null ? FindControllerByTypeName(mvcManager, "ChatMainSharedAreaController") : null;
                object view = chatCtl != null ? GetMember(chatCtl, "viewInstance") : null;
                if (view is UnityEngine.MonoBehaviour mb && mb != null && !mb.gameObject.activeSelf)
                    mb.gameObject.SetActive(true);
            }
            catch (Exception e) { Debug.LogWarning("[HARNESS] ShowChatDefault reactivate failed: " + e.Message); }

            try
            {
                Type controllerT = FindType("DCL.ChatArea.ChatMainSharedAreaController");
                if (controllerT != null && mvcManager != null)
                {
                    MethodInfo issueCmd = null;
                    foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                        if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 0) { issueCmd = mi; break; }
                    object command = issueCmd != null ? issueCmd.Invoke(null, null) : null;
                    if (command != null)
                    {
                        Type[] genArgs = command.GetType().GetGenericArguments();
                        MethodInfo showAsync = null;
                        foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                        if (showAsync != null && genArgs.Length >= 1)
                            showAsync.MakeGenericMethod(genArgs)
                                     .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[HARNESS] ShowChatDefault show failed: " + (e.InnerException?.Message ?? e.Message)); }

            for (int i = 0; i < 10; i++) yield return null;

            RaiseChatBus(mvcManager, "RaiseViewShowEvent");
            for (int i = 0; i < 8; i++) yield return null;
        }

        private static IEnumerator ShowAndFocusChat(object mvcManager)
        {
            yield return ShowChatDefault(mvcManager);

            for (int i = 0; i < 14; i++) yield return null;
        }

        private static bool VerifyShown(object mvcManager, Type keyType, out string err)
        {
            err = null;
            if (mvcManager == null || keyType == null) { err = "no panel key"; return false; }

            object dict = GetPublicProperty(mvcManager, "Controllers");
            if (dict == null)
            {
                object core = GetPrivateField(mvcManager, "core");
                if (core != null) dict = GetPublicProperty(core, "Controllers");
            }
            if (dict == null) { err = "Controllers unavailable"; return false; }
            MethodInfo tryGet = dict.GetType().GetMethod("TryGetValue");
            if (tryGet == null) { err = "TryGetValue unavailable"; return false; }
            object[] a = new object[] { keyType, null };
            bool found = (bool)tryGet.Invoke(dict, a);
            if (!found || a[1] == null) { err = "controller not registered"; return false; }
            string state = GetPublicProperty(a[1], "State")?.ToString() ?? "?";
            if (state == "ViewHidden" || state == "ViewHiding") { err = "view not shown (State=" + state + ")"; return false; }
            return true;
        }

        private static object awaitedResult;
        private static string awaitedError;

        private static IEnumerator EnumerateContent(object mvcManager, Report report)
        {
            object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
            if (explore == null)
            {
                report.actions.Add(new PhaseMarker { label = "data_content", ok = true,
                    error = "expand(non-gating): ExplorePanelController not found" });
                yield break;
            }

            object eventsApi = GetPrivateField(explore, "eventsApiService");
            object placesController = GetMember(explore, "PlacesController");
            object placesResults = placesController != null ? GetMember(placesController, "PlacesResultsController") : null;
            object placesApi = placesResults != null ? GetPrivateField(placesResults, "placesAPIService") : null;
            var ctNone = System.Threading.CancellationToken.None;

            var em = new PhaseMarker { label = "data_events", ok = true };
            report.actions.Add(em);
            if (eventsApi == null) em.error = "expand(non-gating): eventsApiService not found";
            else
            {
                object task = TryInvoke(eventsApi, "GetEventsAsync", new object[] { ctNone, false }, out string ierr);
                if (task == null) em.error = "expand(non-gating): invoke-failed: " + ierr;
                else
                {
                    yield return AwaitUniTask(task);
                    if (awaitedError != null) em.error = "expand(non-gating): " + awaitedError;
                    else em.error = "expand(non-gating): " + SummarizeEvents(awaitedResult);
                }
            }

            yield return EnumeratePlaces(placesApi, "data_places", null, report);
            yield return EnumeratePlaces(placesApi, "data_games", "game", report);
        }

        private static IEnumerator EnumeratePlaces(object placesApi, string label, string categoryOrNull, Report report)
        {
            var pm = new PhaseMarker { label = label, ok = true };
            report.actions.Add(pm);
            if (placesApi == null) { pm.error = "expand(non-gating): placesAPIService not found"; yield break; }

            MethodInfo mi = null;
            foreach (MethodInfo m in placesApi.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (m.Name == "SearchDestinationsAsync") { mi = m; break; }
            if (mi == null) { pm.error = "expand(non-gating): SearchDestinationsAsync not found"; yield break; }

            ParameterInfo[] ps = mi.GetParameters();
            object[] args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(int)) args[i] = (i == 0) ? 0 : 20;
                else if (ps[i].ParameterType == typeof(System.Threading.CancellationToken)) args[i] = System.Threading.CancellationToken.None;
                else if (categoryOrNull != null && ps[i].Name == "category") args[i] = categoryOrNull;
                else args[i] = ps[i].HasDefaultValue ? Type.Missing : null;
            }

            object task = null; string ierr = null;
            try { task = mi.Invoke(placesApi, args); }
            catch (Exception e) { ierr = e.InnerException?.Message ?? e.Message; }
            if (task == null) { pm.error = "expand(non-gating): invoke-failed: " + ierr; yield break; }

            yield return AwaitUniTask(task);
            if (awaitedError != null) { pm.error = "expand(non-gating): " + awaitedError; yield break; }
            pm.error = "expand(non-gating): " + SummarizePlaces(awaitedResult);
        }

        private static IEnumerator AwaitUniTask(object uniTask)
        {
            awaitedResult = null; awaitedError = null;
            object awaiter = null; PropertyInfo isDone = null;
            try
            {
                if (uniTask == null) awaitedError = "null unitask";
                else
                {
                    awaiter = uniTask.GetType().GetMethod("GetAwaiter", Type.EmptyTypes)?.Invoke(uniTask, null);
                    isDone = awaiter?.GetType().GetProperty("IsCompleted");
                }
            }
            catch (Exception e) { awaitedError = "getawaiter: " + (e.InnerException?.Message ?? e.Message); }
            if (awaiter == null || isDone == null) { if (awaitedError == null) awaitedError = "no awaiter"; yield break; }

            float timeout = UnityEngine.Time.realtimeSinceStartup + 15f;
            bool failed = false;
            while (true)
            {
                bool done = false;
                try { done = (bool)isDone.GetValue(awaiter); }
                catch (Exception e) { awaitedError = "iscompleted: " + e.Message; failed = true; }
                if (failed || done) break;
                if (UnityEngine.Time.realtimeSinceStartup > timeout) { awaitedError = "timeout(15s)"; failed = true; break; }
                yield return null;
            }
            if (failed) yield break;

            try { awaitedResult = awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null); }
            catch (Exception e) { awaitedError = "getresult: " + (e.InnerException?.Message ?? e.Message); }
        }

        private static object FindControllerByTypeName(object mvcManager, string typeName)
        {
            object dict = GetPublicProperty(mvcManager, "Controllers");
            if (dict == null) { object core = GetPrivateField(mvcManager, "core"); if (core != null) dict = GetPublicProperty(core, "Controllers"); }
            if (dict == null) return null;
            object values = GetPublicProperty(dict, "Values");
            if (!(values is System.Collections.IEnumerable en)) return null;
            foreach (object c in en)
                if (c != null && c.GetType().Name == typeName) return c;
            return null;
        }

        private static object TryInvoke(object target, string method, object[] args, out string err)
        {
            err = null;
            try
            {
                MethodInfo mi = null;
                foreach (MethodInfo m in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (m.Name == method) { mi = m; break; }
                if (mi == null) { err = method + " not found"; return null; }
                return mi.Invoke(target, args);
            }
            catch (Exception e) { err = e.InnerException?.Message ?? e.Message; return null; }
        }

        private static string SummarizeEvents(object listObj)
        {
            if (!(listObj is System.Collections.IEnumerable en)) return "events: <unreadable>";
            int total = 0, live = 0, shown = 0;
            var sb = new System.Text.StringBuilder();
            foreach (object ev in en)
            {
                total++;
                bool isLive = (GetMember(ev, "live") as bool?) ?? false;
                if (isLive) live++;
                if (isLive && shown < 5)
                {
                    string name = AsciiClean(GetMember(ev, "name") as string ?? "?");
                    sb.Append(name).Append('@').Append(CoordStr(GetMember(ev, "coordinates"))).Append("; ");
                    shown++;
                }
            }
            string head = $"events total={total} live={live}";
            return live > 0 ? head + " | live: " + sb.ToString().TrimEnd(' ', ';') : head;
        }

        private static string SummarizePlaces(object respObj)
        {
            if (respObj == null) return "places: <null response>";

            object total = GetMember(respObj, "total") ?? GetMember(respObj, "Total");
            object data = GetMember(respObj, "data") ?? GetMember(respObj, "Data");
            if (!(data is System.Collections.IEnumerable en)) return "places total=" + total + " | <data unreadable>";
            var sb = new System.Text.StringBuilder();
            int shown = 0, count = 0;
            foreach (object pl in en)
            {
                count++;
                if (shown < 6)
                {
                    string title = AsciiClean(GetMember(pl, "title") as string ?? "?");
                    string pos = AsciiClean(GetMember(pl, "base_position") as string ?? "?");
                    object users = GetMember(pl, "user_count");
                    sb.Append(title).Append('@').Append(pos).Append(" (").Append(users).Append("u); ");
                    shown++;
                }
            }
            return $"total={total} returned={count} | top: " + sb.ToString().TrimEnd(' ', ';');
        }

        private static string CoordStr(object coordsObj)
        {
            if (coordsObj is int[] c && c.Length >= 2) return c[0] + "," + c[1];
            return "?";
        }

        private static string AsciiClean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                if (c >= 0x20 && c <= 0x7E && c != '"' && c != '\\') sb.Append(c);
            string r = sb.ToString().Trim();
            return r.Length == 0 ? "?" : r;
        }

        private static bool TryGetSceneLoaded(object staticContainer, out string sceneName, out bool ready, out string err)
        {
            sceneName = "?"; ready = false; err = null;
            try
            {
                if (staticContainer == null) { err = "staticContainer null"; return false; }
                object scenesCache = GetMember(staticContainer, "ScenesCache");
                if (scenesCache == null) { err = "ScenesCache not found"; return false; }
                object curProp = GetMember(scenesCache, "CurrentScene");
                object facade = curProp != null ? GetMember(curProp, "Value") : null;
                if (facade == null) { err = "no current scene (null)"; return false; }
                try { object r = facade.GetType().GetMethod("IsSceneReady", Type.EmptyTypes)?.Invoke(facade, null); if (r is bool b) ready = b; } catch { }
                sceneName = facade.GetType().Name;
                return true;
            }
            catch (Exception e) { err = e.InnerException?.Message ?? e.Message; return false; }
        }

        private static IEnumerator SwitchToWorldAndShoot(object mvcManager, object staticContainer, object realmNavigator, Report report)
        {
            var wm = new PhaseMarker { label = "world_switch", ok = true };
            report.actions.Add(wm);

            object placesApi = null;
            try
            {
                object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                object pc = explore != null ? GetMember(explore, "PlacesController") : null;
                object prc = pc != null ? GetMember(pc, "PlacesResultsController") : null;
                placesApi = prc != null ? GetPrivateField(prc, "placesAPIService") : null;
            }
            catch (Exception e) { wm.error = "expand(non-gating): places-api lookup failed: " + e.Message; }
            if (placesApi == null || realmNavigator == null)
            { if (wm.error == null) wm.error = "expand(non-gating): placesAPIService/realmNavigator unavailable"; yield break; }

            MethodInfo search = null;
            foreach (MethodInfo m in placesApi.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (m.Name == "SearchDestinationsAsync") { search = m; break; }
            if (search == null) { wm.error = "expand(non-gating): SearchDestinationsAsync not found"; yield break; }
            ParameterInfo[] ps = search.GetParameters();
            object[] args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(int)) args[i] = (i == 0) ? 0 : 30;
                else if (ps[i].ParameterType == typeof(System.Threading.CancellationToken)) args[i] = System.Threading.CancellationToken.None;
                else args[i] = ps[i].HasDefaultValue ? Type.Missing : null;
            }
            object task = null; string ierr = null;
            try { task = search.Invoke(placesApi, args); } catch (Exception e) { ierr = e.InnerException?.Message ?? e.Message; }
            if (task == null) { wm.error = "expand(non-gating): places query invoke-failed: " + ierr; yield break; }
            yield return AwaitUniTask(task);
            if (awaitedError != null) { wm.error = "expand(non-gating): places query: " + awaitedError; yield break; }

            string worldName = null;
            try
            {
                object data = GetMember(awaitedResult, "data") ?? GetMember(awaitedResult, "Data");
                if (data is System.Collections.IEnumerable en)
                    foreach (object pl in en)
                    {
                        string wn = GetMember(pl, "world_name") as string;
                        if (!string.IsNullOrEmpty(wn)) { worldName = wn; break; }
                    }
            }
            catch (Exception e) { wm.error = "expand(non-gating): world-name scan failed: " + e.Message; yield break; }
            if (string.IsNullOrEmpty(worldName)) { wm.error = "expand(non-gating): no live World in places top-30 (skipped)"; yield break; }
            worldName = AsciiClean(worldName);

            object task2 = null; string cerr = null;
            try
            {
                Type urlT = FindType("CommunicationData.URLHelpers.URLDomain");
                MethodInfo fromStr = urlT?.GetMethod("FromString", BindingFlags.Public | BindingFlags.Static);
                if (fromStr == null) wm.error = "expand(non-gating): URLDomain.FromString not found";
                else
                {
                    string url = "https://worlds-content-server.decentraland.org/world/" + worldName.ToLowerInvariant();
                    object urlDom = fromStr.Invoke(null, new object[] { url });
                    MethodInfo change = null;
                    foreach (MethodInfo m in realmNavigator.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        if (m.Name == "TryChangeRealmAsync" && m.GetParameters().Length == 5) { change = m; break; }
                    if (change == null) wm.error = "expand(non-gating): TryChangeRealmAsync(5-arg) not found";
                    else task2 = change.Invoke(realmNavigator, new object[] { urlDom, System.Threading.CancellationToken.None, default(Vector2Int), true, false });
                }
            }
            catch (Exception e) { cerr = e.InnerException?.Message ?? e.Message; }
            if (task2 == null) { if (wm.error == null) wm.error = "expand(non-gating): change-realm invoke-failed: " + cerr; yield break; }

            yield return AwaitUniTask(task2);
            string awaitErr = awaitedError;
            for (int i = 0; i < 90; i++) yield return null;
            yield return CaptureShot("world_" + worldName);

            string realmName = "?", realmKind = "?";
            try
            {
                object realmData = GetMember(staticContainer, "RealmData");
                realmName = GetMember(realmData, "RealmName") as string ?? "?";
                object rtype = GetMember(realmData, "RealmType");
                object rval = rtype != null ? GetMember(rtype, "Value") : null;
                realmKind = rval?.ToString() ?? "?";
            }
            catch (Exception e) { realmKind = "verify-failed:" + e.Message; }
            wm.error = $"expand(non-gating): target='{worldName}' realm='{realmName}' kind={realmKind}" + (awaitErr != null ? " awaitErr=" + awaitErr : "");
        }

        public static void TeleportTo(int x, int y)
        {
            object loader = FindMainSceneLoader();
            if (loader == null) { Debug.LogError("[Harness] TeleportTo: MainSceneLoader not found (in Play?)"); return; }
            object dyn = GetPrivateField(loader, "dynamicWorldContainer");
            object realmNavigator = dyn != null ? GetPublicProperty(dyn, "RealmNavigator") : null;
            if (realmNavigator == null) { Debug.LogError("[Harness] TeleportTo: RealmNavigator not ready (world booted?)"); return; }
            bool ok = TryTeleport(realmNavigator, new Vector2Int(x, y), out string err);
            Debug.Log(ok ? $"[Harness] TeleportTo {x},{y} ok" : $"[Harness] TeleportTo {x},{y} FAILED: {err}");
        }

        private static object FindMainSceneLoader()
        {
            Type t = FindType("Global.Dynamic.MainSceneLoader");
            if (t == null) return null;
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType(t);
#else
            return UnityEngine.Object.FindObjectOfType(t);
#endif
        }

        private static object GetPrivateField(object o, string name)
        {
            if (o == null) return null;
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f.GetValue(o);
            }
            return null;
        }

        private static object GetPublicProperty(object o, string name)
        {
            if (o == null) return null;
            var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p?.GetValue(o);
        }

        private static object GetPublicField(object o, string name)
        {
            if (o == null) return null;
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(o);
            }
            return null;
        }

        private static object GetMember(object o, string name)
        {

            object v = GetPublicProperty(o, name) ?? GetPublicField(o, name) ?? GetPrivateField(o, name);
            if (v != null || o == null) return v;
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                var p = t.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null) { try { return p.GetValue(o); } catch { return null; } }
            }
            return null;
        }

        private static object ReachSelfProfile(object dynamicContainer)
            => GetMember(dynamicContainer, "selfProfile")
               ?? GetPublicProperty(GetPrivateField(dynamicContainer, "profileContainer"), "SelfProfile");

        private static string ReadLoadingStage(object loadingStatus)
        {
            try
            {
                object currentStage = GetPublicProperty(loadingStatus, "CurrentStage");
                if (currentStage == null) return "?";
                object val = GetPublicProperty(currentStage, "Value");
                return val?.ToString() ?? "?";
            }
            catch { return "?"; }
        }

        private static readonly Dictionary<string, Type> typeCache = new();
        private static Type FindType(string fullName)
        {
            if (typeCache.TryGetValue(fullName, out var cached)) return cached;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) { typeCache[fullName] = t; return t; }
            }
            typeCache[fullName] = null;
            return null;
        }

        private class AvatarProbe
        {
            public string name; public string fail;
            public readonly List<Transform> bones = new();
            public readonly List<Quaternion> rot = new();
            public bool Moved()
            {
                for (int i = 0; i < bones.Count; i++)
                    if (bones[i] != null && Quaternion.Angle(rot[i], bones[i].localRotation) > 0.05f) return true;
                return false;
            }
        }

        private static IEnumerator CheckAvatars(string label, Report report, bool previewOnly)
        {
            string err = null;
            var probes = new List<AvatarProbe>();

            Type avatarT  = FindType("DCL.AvatarRendering.AvatarShape.UnityInterface.AvatarBase");
            Type previewT = FindType("DCL.CharacterPreview.CharacterPreviewAvatarContainer");

            if (avatarT == null) err = "AvatarBase type not found";

            if (err == null)
            {
                foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(avatarT))
                {
                    var c = (Component)o;
                    bool isPreview = previewT != null && c.GetComponentInParent(previewT) != null;
                    if (isPreview != previewOnly) continue;

                    var probe = new AvatarProbe { name = c.name };
                    probes.Add(probe);

                    var animator = GetPublicProperty(c, "AvatarAnimator") as Animator;
                    if (animator == null) { probe.fail = "AvatarAnimator is null"; continue; }
                    if (!animator.isActiveAndEnabled) { probe.fail = "animator inactive/disabled"; continue; }
                    if (animator.runtimeAnimatorController == null) { probe.fail = "no animator controller"; continue; }
                    if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate) { probe.fail = "cullingMode=" + animator.cullingMode; continue; }

                    Transform[] sub = animator.GetComponentsInChildren<Transform>();
                    int step = Mathf.Max(1, sub.Length / 32);
                    for (int i = 0; i < sub.Length; i += step) { probe.bones.Add(sub[i]); probe.rot.Add(sub[i].localRotation); }
                }

                if (probes.Count == 0)
                    err = previewOnly ? "no character-preview avatar found with the Backpack open"
                                      : "no world avatar found after spawn";
            }

            if (err == null)
            {
                float until = UnityEngine.Time.realtimeSinceStartup + 1.2f;
                while (UnityEngine.Time.realtimeSinceStartup < until) yield return null;

                foreach (AvatarProbe p in probes)
                    if (p.fail == null && !p.Moved())
                        p.fail = "bones static over 1.2s (" + p.bones.Count + " sampled) - animator not writing transforms";

                int okCount = probes.Count(p => p.fail == null);

                int nNull = 0, nInactive = 0, nNoCtrl = 0, nCulled = 0, nStatic = 0;
                AvatarProbe structuralBad = null;
                foreach (AvatarProbe p in probes)
                {
                    if (p.fail == null) continue;
                    if (p.fail.StartsWith("AvatarAnimator is null")) { nNull++; structuralBad ??= p; }
                    else if (p.fail.StartsWith("animator inactive")) { nInactive++; structuralBad ??= p; }
                    else if (p.fail.StartsWith("no animator controller")) { nNoCtrl++; structuralBad ??= p; }
                    else if (p.fail.StartsWith("cullingMode=")) { nCulled++; structuralBad ??= p; }
                    else if (p.fail.StartsWith("bones static")) nStatic++;
                }

                bool structurallySound = nNull == 0 && nInactive == 0 && nNoCtrl == 0 && nCulled == 0;
                bool pass = previewOnly ? okCount == probes.Count : structurallySound;

                if (!pass)
                {
                    AvatarProbe bad = structuralBad ?? probes.First(p => p.fail != null);
                    err = bad.name + ": " + bad.fail + " (" + okCount + "/" + probes.Count + " avatars ok; "
                        + "structural fails null=" + nNull + " inactive=" + nInactive + " noCtrl=" + nNoCtrl + " culled=" + nCulled + ")";
                }

                Debug.Log("[Harness] " + label + ": avatars=" + probes.Count + " ok=" + okCount
                    + " | fails: nullAnim=" + nNull + " inactive=" + nInactive + " noCtrl=" + nNoCtrl
                    + " culled=" + nCulled + " static=" + nStatic + " (static=informational for world avatars)"
                    + (err == null ? "" : " FAIL: " + err));
            }

            report.actions.Add(new PhaseMarker { label = label, ok = err == null, error = err });

            yield return CaptureShot(label);
        }

        private const float CENSUS_S = 5f;
        private static readonly Vector2Int GENESIS_PLAZA = new Vector2Int(0, 0);
        internal static int commsLastTotalAvatars = -1, commsLastRemoteAvatars = -1;

        public static int CountWorldAvatars(out string names)
        {
            names = "";
            Type avatarT  = FindType("DCL.AvatarRendering.AvatarShape.UnityInterface.AvatarBase");
            Type previewT = FindType("DCL.CharacterPreview.CharacterPreviewAvatarContainer");
            if (avatarT == null) return -1;
            var found = new List<string>();
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(avatarT))
            {
                var c = (Component)o;
                bool isPreview = previewT != null && c.GetComponentInParent(previewT) != null;
                if (isPreview) continue;
                found.Add(c.name);
            }
            names = string.Join(", ", found);
            return found.Count;
        }

        private static IEnumerator RunCommsHoldCoroutine()
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("o") };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            shotIndex = 0;
            try
            {
                if (Directory.Exists(SHOTS_DIR)) Directory.Delete(SHOTS_DIR, true);
                Directory.CreateDirectory(SHOTS_DIR);
            }
            catch (Exception e) { Debug.LogWarning("[Harness] comms: could not reset shots dir: " + e.Message); }

            for (int i = 0; i < 3 && EditorApplication.isPlaying; i++) yield return null;

            object mainSceneLoader = null;
            float findDeadline = UnityEngine.Time.realtimeSinceStartup + 30f;
            while (mainSceneLoader == null && UnityEngine.Time.realtimeSinceStartup < findDeadline)
            {
                mainSceneLoader = FindMainSceneLoader();
                if (mainSceneLoader == null) yield return null;
            }
            if (mainSceneLoader == null) { report.fatal = "comms: no MainSceneLoader"; Finish(report, sw); yield break; }

            object loadingStatus = null;
            float ttiStart = UnityEngine.Time.realtimeSinceStartup;
            float ttiDeadline = ttiStart + LOAD_TIMEOUT_S;
            bool reachedInteractive = false;
            while (UnityEngine.Time.realtimeSinceStartup < ttiDeadline)
            {
                if (loadingStatus == null)
                {
                    var sc = GetPrivateField(mainSceneLoader, "staticContainer");
                    if (sc != null) loadingStatus = GetPublicProperty(sc, "LoadingStatus");
                }
                if (loadingStatus != null)
                {
                    string stage = ReadLoadingStage(loadingStatus);
                    report.lastLoadingStage = stage;
                    if (stage == "Completed") { reachedInteractive = true; break; }
                }
                yield return null;
            }
            report.reachedInteractive = reachedInteractive;
            report.timeToInteractiveSeconds = reachedInteractive ? UnityEngine.Time.realtimeSinceStartup - ttiStart : -1f;
            Debug.Log($"[Harness] COMMS reachedInteractive={reachedInteractive} stage={report.lastLoadingStage} TTI={report.timeToInteractiveSeconds:F1}s");

            object dynamicContainer = GetPrivateField(mainSceneLoader, "dynamicWorldContainer");
            object staticContainer2 = GetPrivateField(mainSceneLoader, "staticContainer");
            object realmNavigator   = dynamicContainer != null ? GetPublicProperty(dynamicContainer, "RealmNavigator") : null;

            SetGameViewSize16x9(captureW, captureH);
            HideDebugPanel(staticContainer2);

            if (!reachedInteractive)
                Debug.LogWarning("[Harness] COMMS not interactive (likely livekit room refused at GlobalPXsLoading); holding anyway to report state.");

            if (realmNavigator != null)
            {
                bool tok = TryTeleport(realmNavigator, GENESIS_PLAZA, out string terr);
                report.actions.Add(new PhaseMarker { label = "comms_teleport_gp", ok = tok, error = tok ? "to 0,0" : terr });
                Debug.Log("[Harness] COMMS teleport to GP 0,0 ok=" + tok + (tok ? "" : " err=" + terr));
                for (int i = 0; i < 300 && tok; i++) yield return null;
            }

            Debug.Log("[Harness] COMMS HOLD begins at GP 0,0. Census every " + CENSUS_S + "s; shot every ~30s. Exit Play to stop.");
            int tick = 0;
            while (EditorApplication.isPlaying)
            {
                int total = CountWorldAvatars(out string names);
                int remote = Mathf.Max(0, total - 1);
                commsLastTotalAvatars = total; commsLastRemoteAvatars = remote;
                Debug.Log($"[Harness] COMMS census tick={tick} world_avatars={total} remote={remote} names=[{names}]");
                if (tick % 6 == 0) yield return CaptureShot("comms_gp");
                float until = UnityEngine.Time.realtimeSinceStartup + CENSUS_S;
                while (UnityEngine.Time.realtimeSinceStartup < until && EditorApplication.isPlaying) yield return null;
                tick++;
            }
            Finish(report, sw);
        }

        private static void SetGameViewSize16x9(int width, int height)
        {
            try
            {
                Assembly editorAsm = typeof(UnityEditor.Editor).Assembly;
                Type sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

                object currentGroupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
                object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)currentGroupType });
                Type groupType = group.GetType();

                int builtin = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null);
                int custom  = (int)groupType.GetMethod("GetCustomCount").Invoke(group, null);
                int total   = builtin + custom;
                MethodInfo getSize = groupType.GetMethod("GetGameViewSize");

                Type gvsType = editorAsm.GetType("UnityEditor.GameViewSize");
                PropertyInfo wProp = gvsType.GetProperty("width");
                PropertyInfo hProp = gvsType.GetProperty("height");

                int index = -1;
                for (int i = 0; i < total; i++)
                {
                    object s = getSize.Invoke(group, new object[] { i });
                    if ((int)wProp.GetValue(s) == width && (int)hProp.GetValue(s) == height) { index = i; break; }
                }

                if (index < 0)
                {
                    Type gvsTypeEnum = editorAsm.GetType("UnityEditor.GameViewSizeType");
                    ConstructorInfo ctor = gvsType.GetConstructor(new[] { gvsTypeEnum, typeof(int), typeof(int), typeof(string) });
                    object newSize = ctor.Invoke(new object[] { Enum.Parse(gvsTypeEnum, "FixedResolution"), width, height, "Harness16x9" });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new object[] { newSize });
                    index = builtin + (int)groupType.GetMethod("GetCustomCount").Invoke(group, null) - 1;
                }

                Type gameViewType = editorAsm.GetType("UnityEditor.GameView");
                EditorWindow gv = EditorWindow.GetWindow(gameViewType, false, null, false);
                MethodInfo sizeSel = gameViewType.GetMethod("SizeSelectionCallback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sizeSel != null) sizeSel.Invoke(gv, new object[] { index, null });
                if (gv != null) gv.Repaint();
                Debug.Log("[Harness] GameView size -> " + width + "x" + height + " (index " + index + ")");
            }
            catch (Exception e) { Debug.LogWarning("[Harness] SetGameViewSize16x9 failed (non-fatal): " + e.Message); }
        }

        private static IEnumerator CaptureShot(string label)
        {

            int secs = atlasSettleSeconds < 1 ? 1 : atlasSettleSeconds;
            for (int s = 1; s <= secs; s++)
            {
                float until = UnityEngine.Time.realtimeSinceStartup + 1f;
                while (UnityEngine.Time.realtimeSinceStartup < until) yield return null;
                WriteShot(s == secs ? label : (label + "_s" + s));
            }
            for (int i = 0; i < 16; i++) yield return null;
        }

        private static void WriteShot(string label)
        {
            string safe = label;
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            string file = Path.Combine(SHOTS_DIR, shotIndex.ToString("D3") + "_" + safe + ".png");
            shotIndex++;
            try { ScreenCapture.CaptureScreenshot(file, captureSuper); }
            catch (Exception e) { Debug.LogWarning("[Harness] screenshot '" + label + "' failed: " + e.Message); }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            totalLogCount++;
            if (type == LogType.Warning)
                lock (warnings) { if (warnings.Count < 500) warnings.Add(new LogEntry { message = condition, stack = Trim(stackTrace), type = type.ToString(), t = Now() }); }
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                lock (errors) { if (errors.Count < 500) errors.Add(new LogEntry { message = condition, stack = Trim(stackTrace), type = type.ToString(), t = Now() }); }
        }

        private static double Now() => EditorApplication.timeSinceStartup;
        private static string Trim(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 600 ? s.Substring(0, 600) : s);

        private static void Finish(Report report, System.Diagnostics.Stopwatch sw)
        {
            Application.logMessageReceivedThreaded -= OnLog;
            report.totalLogMessages = totalLogCount;
            report.warningCount = warnings.Count;
            report.errorCount   = errors.Count;
            report.warnings     = warnings.ToList();
            report.errors       = errors.ToList();
            report.finishedUtc  = DateTime.UtcNow.ToString("o");
            report.totalWallSeconds = sw.Elapsed.TotalSeconds;

            try
            {
                string json = report.ToJson();
                File.WriteAllText(REPORT_PATH, json, new UTF8Encoding(false));
                Debug.Log($"[Harness] Report written to {REPORT_PATH} ({json.Length} bytes). " +
                          $"warnings={report.warningCount} errors={report.errorCount} tti={report.timeToInteractiveSeconds:F1}s");
            }
            catch (Exception e) { Debug.LogError("[Harness] Failed to write report: " + e); }

            if (_exitOnFinish)
            {
                SessionState.SetBool(KEY_QUIT, true);
                SessionState.SetBool(KEY_EXIT, true);
            }
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
        }

        private static int  _savedVSync;
        private static int  _savedTargetFps;
        private static bool _pacingOverridden;
        private static void BeginPerfPacing()
        {
            if (_pacingOverridden) return;
            _savedVSync     = QualitySettings.vSyncCount;
            _savedTargetFps = Application.targetFrameRate;
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = -1;
            _pacingOverridden = true;
        }
        private static void EndPerfPacing()
        {
            if (!_pacingOverridden) return;
            QualitySettings.vSyncCount  = _savedVSync;
            Application.targetFrameRate = _savedTargetFps;
            _pacingOverridden = false;
        }

        private static string VerifyGatingCounters(string tag, ProfilerRecorder cpu, ProfilerRecorder gpu)
        {
            bool cpuOk = cpu.Valid, gpuOk = gpu.Valid;
            if (!cpuOk) Debug.LogError($"[{tag}] CPU counter 'Main Thread' UNAVAILABLE on this build — cpu_ms cannot be measured (not 'fast', MISSING). Check ProfilerCategory/name for this Unity version.");
            if (!gpuOk) Debug.LogWarning($"[{tag}] GPU counter 'GPU Frame Time' unavailable (common in-Editor / GPU profiling off) — gpu_ms will be empty/invalid, NOT zero.");
            return $"cpu={(cpuOk ? "ok" : "UNAVAILABLE")} gpu={(gpuOk ? "ok" : "unavailable")}";
        }

        private static void WriteRunMeta(string csvPath, string tag, string counterNote, string extra)
        {
            try
            {
                string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                string meta =
                    "{" +
                    "\"tag\":" + Q(tag) +
                    ",\"utc\":" + Q(DateTime.UtcNow.ToString("o")) +
                    ",\"unityVersion\":" + Q(Application.unityVersion) +
                    ",\"platform\":" + Q(Application.platform.ToString()) +
                    ",\"graphicsDevice\":" + Q(SystemInfo.graphicsDeviceName) +
                    ",\"graphicsApi\":" + Q(SystemInfo.graphicsDeviceType.ToString()) +
                    ",\"renderPipeline\":" + Q(urp != null ? urp.GetType().Name : "BuiltIn") +
                    ",\"vSyncCount\":" + QualitySettings.vSyncCount +
                    ",\"targetFrameRate\":" + Application.targetFrameRate +
                    ",\"screen\":" + Q(Screen.width + "x" + Screen.height) +
                    ",\"counters\":" + Q(counterNote) +
                    (string.IsNullOrEmpty(extra) ? "" : "," + extra) +
                    "}";
                File.WriteAllText(csvPath + ".meta.json", meta, new UTF8Encoding(false));
            }
            catch (Exception e) { Debug.LogWarning($"[{tag}] meta write failed: " + e.Message); }
        }

        private sealed class BootCtx
        {
            public object loader, staticContainer, loadingStatus, dynamicContainer, mvcManager, realmNavigator;
            public string err;
        }
        private static IEnumerator Bootstrap(BootCtx ctx)
        {
            for (int i = 0; i < 3 && EditorApplication.isPlaying; i++) yield return null;

            float findDeadline = UnityEngine.Time.realtimeSinceStartup + 30f;
            while (ctx.loader == null && UnityEngine.Time.realtimeSinceStartup < findDeadline)
            {
                ctx.loader = FindMainSceneLoader();
                if (ctx.loader == null) yield return null;
            }
            if (ctx.loader == null) { ctx.err = "MainSceneLoader not found (renamed, or boot scene not loaded)"; yield break; }

            float ttiDeadline = UnityEngine.Time.realtimeSinceStartup + LOAD_TIMEOUT_S;
            bool reached = false;
            while (UnityEngine.Time.realtimeSinceStartup < ttiDeadline && EditorApplication.isPlaying)
            {
                if (ctx.staticContainer == null) ctx.staticContainer = GetPrivateField(ctx.loader, "staticContainer");
                if (ctx.staticContainer != null)
                {
                    if (ctx.loadingStatus == null) ctx.loadingStatus = GetPublicProperty(ctx.staticContainer, "LoadingStatus");
                    if (ctx.loadingStatus != null && ReadLoadingStage(ctx.loadingStatus) == "Completed") { reached = true; break; }
                }
                yield return null;
            }
            if (!reached) { ctx.err = "never reached interactive (LoadingStatus.CurrentStage != Completed within " + LOAD_TIMEOUT_S + "s)"; yield break; }

            ctx.dynamicContainer = GetPrivateField(ctx.loader, "dynamicWorldContainer");
            if (ctx.dynamicContainer != null)
            {
                ctx.mvcManager     = GetPublicProperty(ctx.dynamicContainer, "MvcManager");
                ctx.realmNavigator = GetPublicProperty(ctx.dynamicContainer, "RealmNavigator");
            }
            SchemaCheck(ctx.loader, ctx.staticContainer, ctx.dynamicContainer);
        }

        private static void FailMode(string tag, string csvPath, string why)
        {
            Debug.LogError($"[{tag}] aborted: {why}");
            EndPerfPacing();
            try { File.WriteAllText(csvPath, "ERROR," + why, new UTF8Encoding(false)); } catch { }
            if (_exitOnFinish) { SessionState.SetBool(KEY_QUIT, true); SessionState.SetBool(KEY_EXIT, true); }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static IEnumerator RunPerfCoroutine()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (PERF_WINDOWS % 4 != 0) Debug.LogWarning($"[Perf] PERF_WINDOWS={PERF_WINDOWS} is not a multiple of 4 — ABBA blocks will be unbalanced.");

            var ctx = new BootCtx();
            yield return Bootstrap(ctx);
            if (ctx.err != null) { PerfFail(ctx.err); yield break; }
            object mvcManager = ctx.mvcManager;
            if (mvcManager == null) { PerfFail("no MvcManager (dynamicWorldContainer.MvcManager unreachable)"); yield break; }

            if (!TryOpenExplorePanel(mvcManager, "Backpack", null, out string operr)) { PerfFail("open Backpack: " + operr); yield break; }

            float settle = UnityEngine.Time.realtimeSinceStartup + 8f;
            while (UnityEngine.Time.realtimeSinceStartup < settle) yield return null;

            Camera previewCam = FindPreviewCamera();
            if (previewCam == null) { PerfFail("preview camera not found"); yield break; }
            Debug.Log($"[Perf] preview camera = '{previewCam.name}' targetTex='{previewCam.targetTexture?.name}'");

            var mainThread = new ProfilerRecorder(ProfilerCategory.Internal, "Main Thread", 64);
            var gpuFrame   = new ProfilerRecorder(ProfilerCategory.Render,   "GPU Frame Time", 64);
            mainThread.Start(); gpuFrame.Start();
            BeginPerfPacing();
            yield return null; yield return null;
            string counterNote = VerifyGatingCounters("Perf", mainThread, gpuFrame);

            float warmEnd = UnityEngine.Time.realtimeSinceStartup + PERF_WARMUP_S;
            while (UnityEngine.Time.realtimeSinceStartup < warmEnd && EditorApplication.isPlaying) yield return null;

            var rows = new List<string>(8192);
            rows.Add("window,cond,frame_in_window,t_ms,cpu_ms,gpu_ms,drop");
            int frameGlobal = 0;
            bool prevEnabled = previewCam.enabled;
            for (int w = 0; w < PERF_WINDOWS && EditorApplication.isPlaying; w++)
            {
                int block = w % 4;
                bool condA = (block == 0 || block == 3);
                if (previewCam != null) previewCam.enabled = condA;

                int fInWin = 0;
                float wEnd = UnityEngine.Time.realtimeSinceStartup + PERF_WINDOW_S;
                while (UnityEngine.Time.realtimeSinceStartup < wEnd && EditorApplication.isPlaying)
                {
                    double cpu = (mainThread.Valid && mainThread.LastValue > 0) ? mainThread.LastValue * 1e-6 : -1;
                    double gpu = (gpuFrame.Valid && gpuFrame.LastValue > 0) ? gpuFrame.LastValue * 1e-6 : -1;
                    int drop = fInWin < PERF_DROP_FRAMES ? 1 : 0;
                    rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F1},{4:F4},{5:F4},{6}",
                                           w, condA ? "A" : "B", fInWin, sw.Elapsed.TotalMilliseconds, cpu, gpu, drop));
                    fInWin++; frameGlobal++;
                    yield return null;
                }
            }

            if (previewCam != null) previewCam.enabled = prevEnabled;
            EndPerfPacing();
            mainThread.Dispose(); gpuFrame.Dispose();

            try
            {
                File.WriteAllText(PERF_CSV_PATH, string.Join("\n", rows), new UTF8Encoding(false));
                WriteRunMeta(PERF_CSV_PATH, "Perf", counterNote,
                             $"\"windows\":{PERF_WINDOWS},\"windowSeconds\":{PERF_WINDOW_S.ToString(CultureInfo.InvariantCulture)},\"dropFrames\":{PERF_DROP_FRAMES},\"samples\":{rows.Count - 1}");
                Debug.Log($"[Perf] wrote {rows.Count - 1} samples over {PERF_WINDOWS} windows to {PERF_CSV_PATH} ({sw.Elapsed.TotalSeconds:F0}s wall)");
            }
            catch (Exception e) { Debug.LogError("[Perf] CSV write failed: " + e); }

            if (_exitOnFinish) { SessionState.SetBool(KEY_QUIT, true); SessionState.SetBool(KEY_EXIT, true); }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static void PerfFail(string why) => FailMode("Perf", PERF_CSV_PATH, why);

        private static Camera FindPreviewCamera()
        {
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            Camera anyRT = null;
            foreach (var c in cams)
            {
                if (c.targetTexture == null) continue;
                anyRT = c;
                if (c.targetTexture.name == "Preview Texture") return c;
            }
            return anyRT;
        }

        private static IEnumerator RunCpuBreakdownCoroutine()
        {
            var ctx = new BootCtx();
            yield return Bootstrap(ctx);
            if (ctx.err != null) { CpuFail(ctx.err); yield break; }

            float settle = UnityEngine.Time.realtimeSinceStartup + CPU_SETTLE_S;
            while (UnityEngine.Time.realtimeSinceStartup < settle && EditorApplication.isPlaying) yield return null;

            var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
            Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
            var names = new List<string>(); var cats = new List<string>(); var recs = new List<ProfilerRecorder>();
            foreach (var h in handles)
            {
                Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderDescription d;
                try { d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h); }
                catch { continue; }
                if (d.UnitType.ToString() != "TimeNanoseconds") continue;
                ProfilerRecorder r;
                try { r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default); r.Start(); }
                catch { continue; }
                names.Add(d.Name); cats.Add(d.Category.ToString()); recs.Add(r);
                if (recs.Count >= CPU_MAX_MARKERS) break;
            }
            Debug.Log($"[CPU] sampling {recs.Count} time markers");
            if (recs.Count == 0) { CpuFail("no TimeNanoseconds profiler markers found (ProfilerMarkerDataUnit mismatch / profiler off?)"); yield break; }
            BeginPerfPacing();

            float we = UnityEngine.Time.realtimeSinceStartup + CPU_WARMUP_S;
            while (UnityEngine.Time.realtimeSinceStartup < we && EditorApplication.isPlaying) yield return null;

            var sum = new double[recs.Count]; var present = new long[recs.Count];
            int frames = 0;
            float end = UnityEngine.Time.realtimeSinceStartup + CPU_SAMPLE_S;
            while (UnityEngine.Time.realtimeSinceStartup < end && EditorApplication.isPlaying)
            {
                for (int i = 0; i < recs.Count; i++)
                {
                    var r = recs[i];
                    if (r.Valid && r.LastValue > 0) { sum[i] += r.LastValue * 1e-6; present[i]++; }
                }
                frames++;
                yield return null;
            }

            EndPerfPacing();
            var idx = new List<int>();
            for (int i = 0; i < recs.Count; i++) idx.Add(i);
            idx.Sort((a, b) => sum[b].CompareTo(sum[a]));

            var rows = new List<string> { "marker,category,avg_ms_per_frame,frames_present,frames_total,avg_ms_when_present" };
            int written = 0, dropped = 0;
            foreach (int i in idx)
            {
                if (written >= CPU_TOP_N) break;
                double avg = frames > 0 ? sum[i] / frames : 0;
                double avgPresent = present[i] > 0 ? sum[i] / present[i] : 0;
                if (avg <= 0) continue;
                if (avg > 5000) { Debug.LogWarning($"[CPU] dropped suspicious marker '{names[i]}' ({cats[i]}) avg={avg:F1}ms (>5000ms; likely a non-time unit mislabeled) — NOT silently hidden"); dropped++; continue; }
                string nm = names[i].Replace(",", ";").Replace("\n", " ");
                rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F4},{3},{4},{5:F4}", nm, cats[i], avg, present[i], frames, avgPresent));
                written++;
            }
            try
            {
                File.WriteAllText(CPU_CSV_PATH, string.Join("\n", rows), new UTF8Encoding(false));
                WriteRunMeta(CPU_CSV_PATH, "CPU", $"markers={recs.Count}", $"\"markersWritten\":{written},\"markersDropped\":{dropped},\"framesSampled\":{frames}");
                Debug.Log($"[CPU] wrote top {written} markers to {CPU_CSV_PATH} ({frames} frames sampled, {dropped} dropped)");
            }
            catch (Exception e) { Debug.LogError("[CPU] csv write failed: " + e); }

            foreach (var r in recs) r.Dispose();
            if (_exitOnFinish) { SessionState.SetBool(KEY_QUIT, true); SessionState.SetBool(KEY_EXIT, true); }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static void CpuFail(string why) => FailMode("CPU", CPU_CSV_PATH, why);

        private static IEnumerator RunShadowPerfCoroutine()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (PERF_WINDOWS % 4 != 0) Debug.LogWarning($"[Shadow] PERF_WINDOWS={PERF_WINDOWS} is not a multiple of 4 — ABBA blocks will be unbalanced.");

            var ctx = new BootCtx();
            yield return Bootstrap(ctx);
            if (ctx.err != null) { ShadowFail(ctx.err); yield break; }

            float settle = UnityEngine.Time.realtimeSinceStartup + SHADOW_SETTLE_S;
            while (UnityEngine.Time.realtimeSinceStartup < settle && EditorApplication.isPlaying) yield return null;

            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            var orig = new LightShadows[lights.Length];
            int casters = 0;
            for (int i = 0; i < lights.Length; i++) { orig[i] = lights[i].shadows; if (orig[i] != LightShadows.None) casters++; }
            if (casters == 0) { ShadowFail("no shadow-casting lights present"); yield break; }
            Debug.Log($"[Shadow] {lights.Length} lights, {casters} casting shadows");

            var mainThread = new ProfilerRecorder(ProfilerCategory.Internal, "Main Thread", 64);
            var gpuFrame   = new ProfilerRecorder(ProfilerCategory.Render,   "GPU Frame Time", 64);
            mainThread.Start(); gpuFrame.Start();
            BeginPerfPacing();
            yield return null; yield return null;
            string counterNote = VerifyGatingCounters("Shadow", mainThread, gpuFrame);

            float we = UnityEngine.Time.realtimeSinceStartup + PERF_WARMUP_S;
            while (UnityEngine.Time.realtimeSinceStartup < we && EditorApplication.isPlaying) yield return null;

            var rows = new List<string>(8192);
            rows.Add("window,cond,frame_in_window,t_ms,cpu_ms,gpu_ms,drop");
            for (int w = 0; w < PERF_WINDOWS && EditorApplication.isPlaying; w++)
            {
                int block = w % 4; bool condA = (block == 0 || block == 3);
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].shadows = condA ? orig[i] : LightShadows.None;

                int fInWin = 0; float wEnd = UnityEngine.Time.realtimeSinceStartup + PERF_WINDOW_S;
                while (UnityEngine.Time.realtimeSinceStartup < wEnd && EditorApplication.isPlaying)
                {
                    double cpu = (mainThread.Valid && mainThread.LastValue > 0) ? mainThread.LastValue * 1e-6 : -1;
                    double gpu = (gpuFrame.Valid && gpuFrame.LastValue > 0) ? gpuFrame.LastValue * 1e-6 : -1;
                    int drop = fInWin < PERF_DROP_FRAMES ? 1 : 0;
                    rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F1},{4:F4},{5:F4},{6}",
                                           w, condA ? "A" : "B", fInWin, sw.Elapsed.TotalMilliseconds, cpu, gpu, drop));
                    fInWin++; yield return null;
                }
            }
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].shadows = orig[i];
            EndPerfPacing();
            mainThread.Dispose(); gpuFrame.Dispose();

            try
            {
                File.WriteAllText(SHADOW_CSV_PATH, string.Join("\n", rows), new UTF8Encoding(false));
                WriteRunMeta(SHADOW_CSV_PATH, "Shadow", counterNote, $"\"windows\":{PERF_WINDOWS},\"shadowCasters\":{casters},\"samples\":{rows.Count - 1}");
                Debug.Log($"[Shadow] wrote {rows.Count - 1} samples ({casters} shadow lights) to {SHADOW_CSV_PATH}");
            }
            catch (Exception e) { Debug.LogError("[Shadow] csv write failed: " + e); }

            if (_exitOnFinish) { SessionState.SetBool(KEY_QUIT, true); SessionState.SetBool(KEY_EXIT, true); }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static void ShadowFail(string why) => FailMode("Shadow", SHADOW_CSV_PATH, why);

        private static bool SetProp(object o, string name, object val)
        {
            if (o == null) return false;
            var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) return false;
            try { p.SetValue(o, val); return true; } catch { return false; }
        }

        private static IEnumerator RunRenderDecompCoroutine()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (RENDER_WINDOWS_PER_KNOB % 4 != 0) Debug.LogWarning($"[Render] RENDER_WINDOWS_PER_KNOB={RENDER_WINDOWS_PER_KNOB} is not a multiple of 4 — ABBA blocks will be unbalanced.");

            var ctx = new BootCtx();
            yield return Bootstrap(ctx);
            if (ctx.err != null) { RenderFail(ctx.err); yield break; }

            float settle = UnityEngine.Time.realtimeSinceStartup + SHADOW_SETTLE_S;
            while (UnityEngine.Time.realtimeSinceStartup < settle && EditorApplication.isPlaying) yield return null;

            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            var origShadows = new LightShadows[lights.Length];
            int casters = 0;
            for (int i = 0; i < lights.Length; i++) { origShadows[i] = lights[i].shadows; if (origShadows[i] != LightShadows.None) casters++; }

            Camera mainCam = FindMainCamera();
            object camData = mainCam != null ? mainCam.GetComponent("UniversalAdditionalCameraData") : null;
            bool origPostFx = false; bool postFxAvail = false;
            if (camData != null) { object v = GetPublicProperty(camData, "renderPostProcessing"); if (v is bool b) { origPostFx = b; postFxAvail = true; } }

            object urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            int origMsaa = 0; bool msaaAvail = false;
            { object v = urp != null ? GetPublicProperty(urp, "msaaSampleCount") : null; if (v is int mi) { origMsaa = mi; msaaAvail = true; } }
            float origScale = 1f; bool scaleAvail = false;
            { object v = urp != null ? GetPublicProperty(urp, "renderScale") : null; if (v is float fv) { origScale = fv; scaleAvail = true; } }
            bool origBatch = UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching;

            var knobs = new List<string>();
            if (casters > 0) knobs.Add("shadows");
            if (postFxAvail && origPostFx) knobs.Add("postfx");
            knobs.Add("srpbatcher");
            if (msaaAvail && origMsaa > 1) knobs.Add("msaa");
            if (scaleAvail) knobs.Add("renderscale");
            Debug.Log($"[Render] knobs: {string.Join(",", knobs)} | shadowCasters={casters} postfx={origPostFx} msaa={origMsaa} scale={origScale} batch={origBatch}");

            var mainThread = new ProfilerRecorder(ProfilerCategory.Internal, "Main Thread", 64);
            var gpuFrame   = new ProfilerRecorder(ProfilerCategory.Render,   "GPU Frame Time", 64);
            mainThread.Start(); gpuFrame.Start();
            BeginPerfPacing();
            yield return null; yield return null;
            string counterNote = VerifyGatingCounters("Render", mainThread, gpuFrame);

            void Apply(string knob, bool on)
            {
                switch (knob)
                {
                    case "shadows": for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].shadows = on ? origShadows[i] : LightShadows.None; break;
                    case "postfx": SetProp(camData, "renderPostProcessing", on ? origPostFx : false); break;
                    case "srpbatcher": UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = on ? origBatch : false; break;
                    case "msaa": SetProp(urp, "msaaSampleCount", on ? origMsaa : 1); break;
                    case "renderscale": SetProp(urp, "renderScale", on ? origScale : 0.7f); break;
                }
            }
            void RestoreAll()
            {
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].shadows = origShadows[i];
                if (postFxAvail) SetProp(camData, "renderPostProcessing", origPostFx);
                UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = origBatch;
                if (msaaAvail) SetProp(urp, "msaaSampleCount", origMsaa);
                if (scaleAvail) SetProp(urp, "renderScale", origScale);
            }

            float we = UnityEngine.Time.realtimeSinceStartup + PERF_WARMUP_S;
            while (UnityEngine.Time.realtimeSinceStartup < we && EditorApplication.isPlaying) yield return null;

            var rows = new List<string>(16384);
            rows.Add("knob,window,cond,frame_in_window,t_ms,cpu_ms,gpu_ms,drop");
            foreach (string knob in knobs)
            {
                if (!EditorApplication.isPlaying) break;
                RestoreAll();
                for (int w = 0; w < RENDER_WINDOWS_PER_KNOB && EditorApplication.isPlaying; w++)
                {
                    int block = w % 4; bool condA = (block == 0 || block == 3);
                    Apply(knob, condA);
                    int fInWin = 0; float wEnd = UnityEngine.Time.realtimeSinceStartup + PERF_WINDOW_S;
                    while (UnityEngine.Time.realtimeSinceStartup < wEnd && EditorApplication.isPlaying)
                    {
                        double cpu = (mainThread.Valid && mainThread.LastValue > 0) ? mainThread.LastValue * 1e-6 : -1;
                        double gpu = (gpuFrame.Valid && gpuFrame.LastValue > 0) ? gpuFrame.LastValue * 1e-6 : -1;
                        int drop = fInWin < PERF_DROP_FRAMES ? 1 : 0;
                        rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:F1},{5:F4},{6:F4},{7}",
                                               knob, w, condA ? "A" : "B", fInWin, sw.Elapsed.TotalMilliseconds, cpu, gpu, drop));
                        fInWin++; yield return null;
                    }
                }
            }
            RestoreAll();
            EndPerfPacing();
            mainThread.Dispose(); gpuFrame.Dispose();

            try
            {
                File.WriteAllText(RENDER_CSV_PATH, string.Join("\n", rows), new UTF8Encoding(false));
                WriteRunMeta(RENDER_CSV_PATH, "Render", counterNote, $"\"knobs\":\"{string.Join(";", knobs)}\",\"windowsPerKnob\":{RENDER_WINDOWS_PER_KNOB},\"samples\":{rows.Count - 1}");
                Debug.Log($"[Render] wrote {rows.Count - 1} samples over {knobs.Count} knobs to {RENDER_CSV_PATH}");
            }
            catch (Exception e) { Debug.LogError("[Render] csv write failed: " + e); }

            if (_exitOnFinish) { SessionState.SetBool(KEY_QUIT, true); SessionState.SetBool(KEY_EXIT, true); }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static Camera FindMainCamera()
        {
            if (Camera.main != null) return Camera.main;
            Camera best = null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
            {
                if (c.targetTexture != null) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            return best;
        }

        private static void RenderFail(string why) => FailMode("Render", RENDER_CSV_PATH, why);

        private static double Avg(List<double> xs) => xs.Count == 0 ? 0 : xs.Average();
        private static double PercentWorst(List<double> xs, double frac)
        {
            if (xs.Count == 0) return 0;
            int k = Math.Max(1, (int)(xs.Count * frac));
            return xs.OrderByDescending(x => x).Take(k).Average();
        }

        private enum AtlasKind { ExploreSection, Popup, Friends, Custom }
        private sealed class AtlasRoute
        {
            public string name;
            public AtlasKind kind;
            public string controllerType;
            public string section, subTab;
            public System.Func<object> paramFactory;
            public int settleFrames = 18;
        }

        private static IEnumerator RunRoute(AtlasRoute r, object mvc, Report report)
        {
            var m = new PhaseMarker { label = "atlas_" + r.name, ok = false };
            report.actions.Add(m);

            string err = null; bool opened = false;

            if (r.kind == AtlasKind.ExploreSection)
            {
                yield return OpenExplorePanelDirectCo(mvc, r.section, null, r.subTab);
                opened = openDirectErr == null; err = openDirectErr;
            }
            else
            {
                try
                {
                    object param = r.kind == AtlasKind.Popup && r.paramFactory != null ? r.paramFactory() : null;
                    switch (r.kind)
                    {
                        case AtlasKind.Friends: opened = TryOpenFriendsPanel(mvc, r.subTab, out err); break;
                        case AtlasKind.Popup:   opened = TryShowPanelByName(mvc, r.controllerType, param, out err); break;
                    }
                }
                catch (System.Exception e) { err = e.InnerException?.Message ?? e.Message; }
            }
            if (!opened) { m.error = "open-failed: " + (err ?? "?"); yield break; }

            for (int i = 0; i < r.settleFrames; i++) yield return null;
            yield return CaptureShot(r.name);

            string verr = "panel key unavailable";
            bool shown = lastPanelKey != null && VerifyShown(mvc, lastPanelKey, out verr);
            m.ok = shown;
            m.error = shown ? "shown" : "not-shown: " + (verr ?? "?");
        }

        private static AtlasRoute Folded(string name)
        {
            switch (name)
            {
                case "settings":      return new AtlasRoute { name = "settings",      kind = AtlasKind.ExploreSection, section = "Settings", subTab = "GRAPHICS", settleFrames = 24 };
                case "map":           return new AtlasRoute { name = "map",           kind = AtlasKind.ExploreSection, section = "Navmap",   settleFrames = 18 };
                case "backpack":      return new AtlasRoute { name = "backpack",      kind = AtlasKind.ExploreSection, section = "Backpack", settleFrames = 18 };
                case "controls":      return new AtlasRoute { name = "controls",      kind = AtlasKind.ExploreSection, section = "Settings", subTab = "CONTROLS", settleFrames = 18 };
                case "marketplace":   return new AtlasRoute { name = "marketplace",   kind = AtlasKind.ExploreSection, section = "Backpack", settleFrames = 300 };
                case "friends":       return new AtlasRoute { name = "friends",       kind = AtlasKind.Friends,        subTab = "FRIENDS",   settleFrames = 24 };
                case "voice":         return new AtlasRoute { name = "voice",         kind = AtlasKind.Popup, controllerType = "DCL.VoiceChat.UI.NearbyVoicePanelController", settleFrames = 18 };
                case "profilewidget": return new AtlasRoute { name = "profilewidget", kind = AtlasKind.Popup, controllerType = "DCL.UI.Profiles.ProfileMenuController", settleFrames = 18 };
                case "emotewheel":    return new AtlasRoute { name = "emotewheel",    kind = AtlasKind.Popup, controllerType = "DCL.EmotesWheel.EmotesWheelController", settleFrames = 18 };
                case "skybox":        return new AtlasRoute { name = "skybox",        kind = AtlasKind.Popup, controllerType = "DCL.UI.Skybox.SkyboxMenuController", settleFrames = 18 };
                default: throw new ArgumentException("unknown folded atlas route: " + name);
            }
        }

        private static IEnumerator AtlasCapture_explore(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_explore", ok = true };
            report.actions.Add(m);

            string err = null;
            try
            {
                if (mvcManager == null) err = "explore: mvcManager null";
                else if (!TryOpenExplorePanel(mvcManager, "Events", null, out string openErr))
                    err = "explore: " + openErr;
            }
            catch (System.Exception e) { err = "explore: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            object stateService = null;
            string pollNote = null;
            try
            {
                object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                object eventsController = explore != null ? GetMember(explore, "EventsController") : null;
                stateService = eventsController != null ? GetPrivateField(eventsController, "eventsStateService") : null;
                if (stateService == null) pollNote = "no-state-service(fallback-settle)";
            }
            catch (System.Exception e) { pollNote = "resolve-failed(fallback-settle): " + (e.InnerException?.Message ?? e.Message); }

            int loadedCount = 0;
            if (stateService != null)
            {
                const int maxFrames = 360;
                for (int i = 0; i < maxFrames; i++)
                {
                    int c = 0;
                    try
                    {
                        object dict = GetPrivateField(stateService, "currentEvents");
                        object cnt = dict != null ? GetMember(dict, "Count") : null;
                        if (cnt is int ci) c = ci;
                    }
                    catch { c = 0; }
                    loadedCount = c;
                    if (c > 0) break;
                    yield return null;
                }

                for (int i = 0; i < 48; i++) yield return null;
                pollNote = loadedCount > 0 ? ("events-loaded=" + loadedCount) : "poll-timeout(empty-or-no-events)";
            }
            else
            {

                for (int i = 0; i < 270; i++) yield return null;
            }

            yield return CaptureShot("explore");

            string verifyErr = "no panel key";
            bool shown = lastPanelKey != null && VerifyShown(mvcManager, lastPanelKey, out verifyErr);
            m.error = shown ? ("shown" + (pollNote != null ? " (" + pollNote + ")" : "")) : ("not-shown: " + (verifyErr ?? "no panel key"));
        }

        private static IEnumerator AtlasCapture_places(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_places", ok = true };
            report.actions.Add(m);

            string err = null;
            if (mvcManager == null) err = "places: mvcManager null";
            if (err == null)
            {
                yield return OpenExplorePanelDirectCo(mvcManager, "Places", null, null);
                if (openDirectErr != null) err = "places: " + openDirectErr;
            }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            bool loaded = false;
            int placesCount = 0;
            for (int i = 0; i < 360 && !loaded; i++)
            {
                try
                {
                    object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                    object placesCtrl = explore != null ? GetMember(explore, "PlacesController") : null;
                    object resultsCtrl = placesCtrl != null ? GetMember(placesCtrl, "PlacesResultsController") : null;
                    if (resultsCtrl != null)
                    {
                        object stateSvc = GetMember(resultsCtrl, "placesStateService");
                        object current = stateSvc != null ? GetMember(stateSvc, "CurrentPlaces") : null;
                        object cnt = current != null ? GetMember(current, "Count") : null;
                        placesCount = (cnt as int?) ?? 0;
                        bool gridLoading = (GetMember(resultsCtrl, "isPlacesGridLoadingItems") as bool?) ?? true;
                        if (placesCount > 0 && !gridLoading) loaded = true;
                    }
                }
                catch {  }
                yield return null;
            }

            int extra = loaded ? 90 : 240;
            for (int i = 0; i < extra; i++) yield return null;

            yield return CaptureShot("places");

            string verifyErr = "no panel key";
            bool shown = lastPanelKey != null && VerifyShown(mvcManager, lastPanelKey, out verifyErr);
            m.error = shown ? "shown" : ("not-shown: " + (verifyErr ?? "no panel key"));
        }

        private static IEnumerator AtlasCapture_events(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_events", ok = true };
            report.actions.Add(m);

            string err = null;
            if (mvcManager == null) err = "events: mvcManager null";
            if (err == null)
            {

                yield return OpenExplorePanelDirectCo(mvcManager, "Events", null, null);
                if (openDirectErr != null) err = "events: " + openDirectErr;
            }
            if (err != null) { m.error = err; yield break; }

            object eventsState = null;
            try
            {
                object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                object eventsController = explore != null ? GetPublicProperty(explore, "EventsController") : null;
                if (eventsController != null) eventsState = GetPrivateField(eventsController, "eventsStateService");
            }
            catch (System.Exception) { eventsState = null; }

            int loadedCount = 0;
            int maxFrames = 360;
            for (int i = 0; i < maxFrames; i++)
            {
                int c = 0;
                try
                {
                    if (eventsState != null)
                    {
                        object dict = GetPrivateField(eventsState, "currentEvents");
                        object cnt = dict != null ? GetMember(dict, "Count") : null;
                        if (cnt is int ci) c = ci;
                    }
                }
                catch (System.Exception) { c = 0; }
                loadedCount = c;
                if (loadedCount > 0) break;
                yield return null;
            }

            int settle = loadedCount > 0 ? 90 : 240;
            for (int i = 0; i < settle; i++) yield return null;

            yield return CaptureShot("events");

            string verifyErr = "no panel key";
            bool shown = lastPanelKey != null && VerifyShown(mvcManager, lastPanelKey, out verifyErr);
            if (shown)
                m.error = loadedCount > 0 ? "shown" : "shown:degraded-no-events-loaded";
            else
                m.error = "not-shown: " + (verifyErr ?? "no panel key");
        }

        private static IEnumerator AtlasCapture_backpackemotes(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_backpackemotes", ok = true };
            report.actions.Add(m);

            string err = null;
            if (mvcManager == null) err = "backpackemotes: mvcManager null";
            if (err == null)
            {
                yield return OpenExplorePanelDirectCo(mvcManager, "Backpack", "Emotes", null);
                if (openDirectErr != null) err = "backpackemotes: " + openDirectErr;
            }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            if (lastPanelKey != null && !VerifyShown(mvcManager, lastPanelKey, out string verifyErr))
            {
                m.error = "backpackemotes: " + verifyErr;
                yield break;
            }

            yield return CaptureShot("backpackemotes");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_backpackoutfits(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_backpackoutfits", ok = true };
            report.actions.Add(m);

            string err = null;
            try
            {
                Type controllerT = FindType("DCL.ExplorePanel.ExplorePanelController");
                Type paramT      = FindType("DCL.ExplorePanel.ExplorePanelParameter");
                Type sectionsT   = FindType("DCL.UI.ExploreSections");
                if (controllerT == null) err = "backpackoutfits: ExplorePanelController type not found";
                else if (paramT == null) err = "backpackoutfits: ExplorePanelParameter type not found";
                else if (sectionsT == null) err = "backpackoutfits: ExploreSections type not found";
                else
                {
                    object backpackSection = Enum.Parse(sectionsT, "Backpack");

                    ConstructorInfo ctor = paramT.GetConstructors()[0];
                    object[] ctorArgs = new object[ctor.GetParameters().Length];
                    ctorArgs[0] = backpackSection;
                    object param = ctor.Invoke(ctorArgs);

                    MethodInfo issue = controllerT.GetMethod("IssueCommand",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (issue == null) err = "backpackoutfits: IssueCommand not found on ExplorePanelController";
                    else
                    {
                        object command = issue.Invoke(null, new[] { param });
                        if (command == null) err = "backpackoutfits: IssueCommand returned null";
                        else
                        {
                            MethodInfo showAsync = null;
                            foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                            if (showAsync == null) err = "backpackoutfits: ShowAsync not found on mvcManager";
                            else
                            {
                                Type[] genArgs = command.GetType().GetGenericArguments();
                                showAsync.MakeGenericMethod(genArgs)
                                         .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                                Type ifaceOpen = FindType("MVC.IController`2");
                                lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "backpackoutfits: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 48; i++) yield return null;

            object toggle = null;
            try
            {
                object explorePanelCtl = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                if (explorePanelCtl == null) err = "backpackoutfits: ExplorePanelController not found at runtime";
                else
                {
                    object backpackCtl = GetMember(explorePanelCtl, "backpackController");
                    if (backpackCtl == null) err = "backpackoutfits: backpackController field not found";
                    else
                    {
                        object avatarCtl = GetMember(backpackCtl, "avatarController");
                        if (avatarCtl == null) err = "backpackoutfits: avatarController field not found";
                        else
                        {
                            object avatarView = GetMember(avatarCtl, "view");
                            if (avatarView == null) err = "backpackoutfits: AvatarController.view not found";
                            else
                            {
                                object outfitsTabSelector = GetMember(avatarView, "OutfitsTabSelector");
                                if (outfitsTabSelector == null) err = "backpackoutfits: OutfitsTabSelector not found on AvatarView";
                                else
                                {
                                    toggle = GetMember(outfitsTabSelector, "TabSelectorToggle");
                                    if (toggle == null) err = "backpackoutfits: TabSelectorToggle not found on OutfitsTabSelector";
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "backpackoutfits: " + (e.InnerException?.Message ?? e.Message); }

            if (err == null && toggle != null)
            {
                try
                {
                    PropertyInfo isOnProp = toggle.GetType().GetProperty("isOn", BindingFlags.Public | BindingFlags.Instance);
                    if (isOnProp == null) err = "backpackoutfits: isOn property not found on Toggle";
                    else
                    {

                        MethodInfo setWithoutNotify = toggle.GetType().GetMethod("SetIsOnWithoutNotify", BindingFlags.Public | BindingFlags.Instance);
                        if (setWithoutNotify != null) setWithoutNotify.Invoke(toggle, new object[] { false });
                        isOnProp.SetValue(toggle, true);

                        object onValueChanged = GetMember(toggle, "onValueChanged");
                        if (onValueChanged != null)
                        {
                            MethodInfo invokeMethod = onValueChanged.GetType().GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
                            if (invokeMethod != null) invokeMethod.Invoke(onValueChanged, new object[] { true });
                        }
                    }
                }
                catch (System.Exception e) { err = "backpackoutfits: " + (e.InnerException?.Message ?? e.Message); }
            }

            for (int i = 0; i < 90; i++) yield return null;

            yield return CaptureShot("backpackoutfits");

            m.error = err != null ? err : "shown";
        }

        private static IEnumerator AtlasCapture_placedetail(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_placedetail", ok = true };
            report.actions.Add(m);

            string err = null;
            object placesApi = null;
            MethodInfo searchMethod = null;
            object[] searchArgs = null;

            try
            {

                object explorePanelCtl = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                if (explorePanelCtl == null) err = "placedetail: ExplorePanelController not found";

                object placesController = err != null ? null : GetMember(explorePanelCtl, "PlacesController");
                if (err == null && placesController == null) err = "placedetail: PlacesController not found";

                object placesResultsCtl = err != null ? null : GetMember(placesController, "PlacesResultsController");
                if (err == null && placesResultsCtl == null) err = "placedetail: PlacesResultsController not found";

                placesApi = err != null ? null : GetPrivateField(placesResultsCtl, "placesAPIService");
                if (err == null && placesApi == null) err = "placedetail: placesAPIService not found";

                if (err == null)
                {
                    foreach (MethodInfo mi in placesApi.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        if (mi.Name == "SearchDestinationsAsync") { searchMethod = mi; break; }
                    if (searchMethod == null) err = "placedetail: SearchDestinationsAsync not found";
                }

                if (err == null)
                {

                    ParameterInfo[] ps = searchMethod.GetParameters();
                    searchArgs = new object[ps.Length];
                    int intSeen = 0;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (ps[i].ParameterType == typeof(int)) searchArgs[i] = (intSeen++ == 0) ? 0 : 20;
                        else if (ps[i].ParameterType == typeof(System.Threading.CancellationToken)) searchArgs[i] = System.Threading.CancellationToken.None;
                        else searchArgs[i] = ps[i].HasDefaultValue ? Type.Missing : null;
                    }
                }
            }
            catch (System.Exception e) { err = "placedetail: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            object task = null;
            try { task = searchMethod.Invoke(placesApi, searchArgs); }
            catch (System.Exception e) { err = "placedetail: invoke SearchDestinationsAsync: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }
            if (task == null) { m.error = "placedetail: SearchDestinationsAsync returned null"; yield break; }

            yield return AwaitUniTask(task);
            if (awaitedError != null) { m.error = "placedetail: SearchDestinationsAsync failed: " + awaitedError; yield break; }

            object placeInfo = null;
            object command = null;
            MethodInfo showAsync = null;
            Type[] genArgs = null;
            try
            {
                object response = awaitedResult;
                if (response == null) err = "placedetail: no response from SearchDestinationsAsync";

                object data = null;
                if (err == null)
                {

                    Type respIface = FindType("DCL.PlacesAPIService.PlacesData+IPlacesAPIResponse");
                    if (respIface != null)
                    {
                        PropertyInfo dataProp = respIface.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
                        if (dataProp != null)
                        {
                            try { data = dataProp.GetValue(response); } catch { data = null; }
                        }
                    }

                    if (data == null) data = GetPublicField(response, "data");
                    if (data == null) err = "placedetail: could not read IPlacesAPIResponse.Data";
                }

                System.Collections.IEnumerable seq = data as System.Collections.IEnumerable;
                if (err == null && seq == null) err = "placedetail: response Data not enumerable";

                if (err == null)
                {
                    foreach (object item in seq) { placeInfo = item; break; }
                    if (placeInfo == null) err = "placedetail: no places in response data";
                }

                object paramObj = null;
                if (err == null)
                {
                    Type paramType = FindType("DCL.Places.PlaceDetailPanelParameter");
                    if (paramType == null) err = "placedetail: PlaceDetailPanelParameter type not found";
                    else
                    {
                        ConstructorInfo ctor = null;
                        foreach (ConstructorInfo ci in paramType.GetConstructors())
                            if (ci.GetParameters().Length == 4) { ctor = ci; break; }
                        if (ctor == null) err = "placedetail: PlaceDetailPanelParameter ctor(4) not found";
                        else paramObj = ctor.Invoke(new object[] { placeInfo, null, null, null });
                    }
                }
                if (err == null && paramObj == null) err = "placedetail: parameter construction returned null";

                if (err == null)
                {
                    Type controllerT = FindType("DCL.Places.PlaceDetailPanelController");
                    if (controllerT == null) err = "placedetail: PlaceDetailPanelController not found";
                    else
                    {
                        MethodInfo issueCmd = null;
                        foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                            if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 1) { issueCmd = mi; break; }
                        if (issueCmd == null) err = "placedetail: IssueCommand(param) not found";
                        else command = issueCmd.Invoke(null, new[] { paramObj });
                    }
                }
                if (err == null && command == null) err = "placedetail: IssueCommand returned null";

                if (err == null)
                {
                    foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                    if (showAsync == null) err = "placedetail: ShowAsync not found";
                    else genArgs = command.GetType().GetGenericArguments();
                }

                if (err == null)
                {
                    showAsync.MakeGenericMethod(genArgs)
                             .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });

                    Type ifaceOpen = FindType("MVC.IController`2");
                    lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                }
            }
            catch (System.Exception e) { err = "placedetail: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            if (lastPanelKey != null && !VerifyShown(mvcManager, lastPanelKey, out string verr))
            {
                m.error = "placedetail: not-shown: " + verr;
                yield break;
            }

            yield return CaptureShot("placedetail");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_eventdetail(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_eventdetail", ok = true };
            report.actions.Add(m);

            string err = null;
            object eventsApi = null;

            try
            {
                object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                if (explore != null)
                    eventsApi = GetPrivateField(explore, "eventsApiService");

                if (eventsApi == null)
                    err = "eventdetail: eventsApiService not found";
            }
            catch (System.Exception e) { err = "eventdetail: " + (e.InnerException?.Message ?? e.Message); }

            if (err != null)
            {
                m.error = err + "; skipped:no-api";
                yield return CaptureShot("eventdetail");
                yield break;
            }

            object eventTask = null;
            try
            {
                System.DateTime now = System.DateTime.UtcNow;
                System.DateTime toDate = now.AddDays(7);
                System.Reflection.MethodInfo getEventsMethod = null;
                foreach (var mi in eventsApi.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (mi.Name == "GetEventsByDateRangeAsync") { getEventsMethod = mi; break; }
                }
                if (getEventsMethod != null)
                    eventTask = getEventsMethod.Invoke(eventsApi, new object[] { now, (object)toDate, (object)true, System.Threading.CancellationToken.None });
                else
                    err = "eventdetail: GetEventsByDateRangeAsync not found";
            }
            catch (System.Exception e) { err = "eventdetail: " + (e.InnerException?.Message ?? e.Message); }

            if (err != null || eventTask == null)
            {
                m.error = (err ?? "eventdetail: event fetch returned null") + "; skipped:no-events";
                yield return CaptureShot("eventdetail");
                yield break;
            }

            yield return AwaitUniTask(eventTask);
            if (awaitedError != null || awaitedResult == null)
            {
                m.error = "eventdetail: event fetch await failed: " + (awaitedError ?? "no result") + "; skipped:fetch-error";
                yield return CaptureShot("eventdetail");
                yield break;
            }

            object parameter = null;
            try
            {
                object eventData = null;
                if (awaitedResult is System.Collections.IEnumerable eventList)
                {
                    foreach (object ev in eventList) { eventData = ev; break; }
                }

                if (eventData == null)
                    err = "eventdetail: no events available; skipped:empty-list";
                else
                {
                    Type paramType = FindType("DCL.Communities.EventInfo.EventDetailPanelParameter");
                    Type eventDtoType = FindType("DCL.EventsApi.IEventDTO");
                    Type placeInfoType = FindType("DCL.PlacesAPIService.PlacesData+PlaceInfo");
                    Type eventCardType = FindType("DCL.Events.EventCardView");

                    if (paramType != null && eventDtoType != null && placeInfoType != null && eventCardType != null)
                    {

                        var ctor = paramType.GetConstructor(new[] { eventDtoType, placeInfoType, eventCardType });
                        if (ctor != null)
                            parameter = ctor.Invoke(new[] { eventData, null, null });
                        else
                            err = "eventdetail: EventDetailPanelParameter ctor not found";
                    }
                    else
                        err = "eventdetail: required types not resolved";
                }
            }
            catch (System.Exception e) { err = "eventdetail: " + (e.InnerException?.Message ?? e.Message); }

            if (err != null || parameter == null)
            {
                m.error = (err ?? "eventdetail: parameter construction failed; skipped:param-failed");
                if (m.error.IndexOf("skipped:") < 0) m.error += "; skipped:param-failed";
                yield return CaptureShot("eventdetail");
                yield break;
            }

            string showErr;
            if (!TryShowPanelByName(mvcManager, "DCL.Communities.EventInfo.EventDetailPanelController", parameter, out showErr))
            {
                m.error = "eventdetail: show panel failed: " + showErr + "; skipped:show-failed";
                yield return CaptureShot("eventdetail");
                yield break;
            }

            for (int i = 0; i < 18; i++) yield return null;
            HideChat(mvcManager);
            yield return CaptureShot("eventdetail");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_navigation(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_navigation", ok = true };
            report.actions.Add(m);

            string err = null;
            object searchTask = null;

            try
            {

                if (!TryOpenExplorePanel(mvcManager, "Navmap", null, out string openErr))
                {
                    err = "navigation: panel open failed: " + openErr;
                }
                else
                {

                    object navmapController = FindControllerByTypeName(mvcManager, "NavmapController");
                    object navmapBus = navmapController != null ? GetPrivateField(navmapController, "navmapBus") : null;

                    if (navmapBus != null)
                    {
                        Type searchParamsType = FindType("DCL.Navmap.INavmapBus+SearchPlaceParams");
                        Type filterType = FindType("DCL.Navmap.NavmapSearchPlaceFilter");
                        Type sortingType = FindType("DCL.Navmap.NavmapSearchPlaceSorting");

                        if (searchParamsType != null && filterType != null && sortingType != null)
                        {

                            MethodInfo factoryMethod = searchParamsType.GetMethod("CreateWithDefaultParams",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                            if (factoryMethod != null)
                            {
                                object allFilter = Enum.Parse(filterType, "All");
                                object mostActiveSorting = Enum.Parse(sortingType, "MostActive");

                                object searchParams = factoryMethod.Invoke(null, new object[]
                                {
                                    0,
                                    50,
                                    null,
                                    allFilter,
                                    mostActiveSorting,
                                    null,
                                });

                                MethodInfo searchMethod = navmapBus.GetType().GetMethod("SearchForPlaceAsync",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (searchMethod != null)
                                {
                                    searchTask = searchMethod.Invoke(navmapBus, new object[]
                                    {
                                        searchParams,
                                        System.Threading.CancellationToken.None,
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "navigation: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            if (!VerifyShown(mvcManager, lastPanelKey, out string verifyErr))
                m.error = "navigation(non-gating): not-shown: " + verifyErr;

            if (searchTask != null)
            {
                yield return AwaitUniTask(searchTask);
                if (awaitedError != null) m.error = "navigation(non-gating): search error: " + awaitedError;
            }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("navigation");
            if (m.error == null) m.error = "shown";
        }

        private static IEnumerator AtlasCapture_iteminfo(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_iteminfo", ok = true };
            report.actions.Add(m);

            string err = null;
            object backpackController = null;
            object avatarController = null;
            object gridController = null;
            object backpackCommandBus = null;

            yield return OpenExplorePanelDirectCo(mvcManager, "Backpack", null, null);
            if (openDirectErr != null) err = "iteminfo: open backpack failed: " + openDirectErr;

            for (int i = 0; i < 30; i++) yield return null;

            try
            {
                if (err == null)
                {
                    object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                    if (explore != null)
                        backpackController = GetPrivateField(explore, "backpackController");
                    if (backpackController == null)
                        err = "iteminfo: BackpackController not reachable from ExplorePanelController";
                }

                if (err == null)
                {
                    backpackCommandBus = GetPrivateField(backpackController, "backpackCommandBus");
                    avatarController = GetPrivateField(backpackController, "avatarController");
                    if (avatarController != null)
                        gridController = GetPrivateField(avatarController, "backpackGridController");

                    if (gridController == null)
                        err = "iteminfo: BackpackGridController not reachable";
                    else if (backpackCommandBus == null)
                        err = "iteminfo: backpackCommandBus not found";
                }
            }
            catch (System.Exception e) { err = "iteminfo: reach: " + (e.InnerException?.Message ?? e.Message); }

            var candidates = new System.Collections.Generic.List<string>();
            if (err == null)
            {
                for (int i = 0; i < 360 && candidates.Count == 0; i++)
                {
                    try
                    {

                        object pool = GetPrivateField(gridController, "usedPoolItems");
                        if (pool is System.Collections.IDictionary dict)
                        {
                            foreach (object key in dict.Keys)
                            {
                                if (key == null) continue;
                                string s = key.ToString();

                                if (string.IsNullOrEmpty(s)) continue;
                                if (s.Length <= 2 && int.TryParse(s, out _)) continue;
                                if (!candidates.Contains(s)) candidates.Add(s);
                            }
                        }

                        if (candidates.Count == 0)
                        {
                            object list = GetPrivateField(gridController, "results");
                            for (int pass = 0; pass < 2 && candidates.Count == 0; pass++)
                            {
                                if (list is System.Collections.IEnumerable items)
                                {
                                    foreach (object item in items)
                                    {
                                        if (item == null) continue;
                                        MethodInfo getUrn = item.GetType().GetMethod("GetUrn",
                                            BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                                        object urnObj = getUrn?.Invoke(item, null);
                                        if (urnObj == null) continue;
                                        string s = urnObj.ToString();
                                        if (!string.IsNullOrEmpty(s) && !candidates.Contains(s)) candidates.Add(s);
                                    }
                                }
                                if (candidates.Count == 0)
                                    list = GetPrivateField(gridController, "currentPageWearables");
                            }
                        }
                    }
                    catch { candidates.Clear(); }

                    if (candidates.Count == 0) yield return null;
                }
            }

            if (err == null && candidates.Count == 0)
                err = "iteminfo: grid produced no wearable (empty/loading inventory)";

            Type cmdType = null;
            MethodInfo send = null;
            if (err == null)
            {
                try
                {
                    cmdType = FindType("DCL.Backpack.BackpackBus.BackpackSelectWearableCommand");
                    if (cmdType == null)
                        err = "iteminfo: BackpackSelectWearableCommand type not found";
                    else
                    {

                        send = backpackCommandBus.GetType().GetMethod(
                            "SendCommand", BindingFlags.Public | BindingFlags.Instance, null, new[] { cmdType }, null);
                        if (send == null)
                            err = "iteminfo: SendCommand(BackpackSelectWearableCommand) overload not found";
                    }
                }
                catch (System.Exception e) { err = "iteminfo: resolve: " + (e.InnerException?.Message ?? e.Message); }
            }

            bool infoShown = false;
            if (err == null)
            {
                for (int c = 0; c < candidates.Count && !infoShown; c++)
                {
                    string wearableId = candidates[c];

                    try
                    {

                        object selectCommand = Activator.CreateInstance(cmdType, new object[] { wearableId, null });
                        send.Invoke(backpackCommandBus, new[] { selectCommand });
                    }
                    catch (System.Exception e) { m.error = "iteminfo: dispatch: " + (e.InnerException?.Message ?? e.Message); }

                    for (int i = 0; i < 90 && !infoShown; i++)
                    {
                        bool full = false, empty = true;
                        try
                        {
                            object infoPanelController = GetPrivateField(avatarController, "backpackInfoPanelController");
                            object view = infoPanelController != null ? GetPrivateField(infoPanelController, "view") : null;
                            object fullPanel = view != null ? GetMember(view, "FullPanel") : null;
                            object emptyPanel = view != null ? GetMember(view, "EmptyPanel") : null;
                            if (fullPanel != null)
                                full = (bool)(fullPanel.GetType()
                                    .GetProperty("activeInHierarchy", BindingFlags.Public | BindingFlags.Instance)
                                    ?.GetValue(fullPanel) ?? false);
                            if (emptyPanel != null)
                                empty = (bool)(emptyPanel.GetType()
                                    .GetProperty("activeInHierarchy", BindingFlags.Public | BindingFlags.Instance)
                                    ?.GetValue(emptyPanel) ?? true);
                        }
                        catch { full = false; empty = true; }

                        if (full && !empty) { infoShown = true; break; }
                        yield return null;
                    }
                }
            }
            else
            {

                for (int i = 0; i < 30; i++) yield return null;
            }

            for (int i = 0; i < 30; i++) yield return null;

            if (err != null)
                m.error = err;
            else
                m.error = infoShown ? "shown" : "atlas: info panel inactive (captured backpack grid)";

            yield return CaptureShot("iteminfo");
        }

        private static IEnumerator AtlasCapture_mapfilters(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_mapfilters", ok = true };
            report.actions.Add(m);

            string err = null;
            Type explorePanelT = null;
            object param = null;

            try
            {
                Type paramT = FindType("DCL.ExplorePanel.ExplorePanelParameter");
                Type sectionsT = FindType("DCL.UI.ExploreSections");
                explorePanelT = FindType("DCL.ExplorePanel.ExplorePanelController");
                if (paramT == null || sectionsT == null || explorePanelT == null)
                { err = "mapfilters: types not found"; }
                else
                {
                    object section = Enum.Parse(sectionsT, "Navmap");
                    ConstructorInfo ctor = paramT.GetConstructors()[0];
                    object[] ctorArgs = new object[ctor.GetParameters().Length];
                    ctorArgs[0] = section;
                    param = ctor.Invoke(ctorArgs);
                }
            }
            catch (System.Exception e) { err = "mapfilters: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            yield return CloseOpenPanels(mvcManager);

            try
            {
                if (!TryShowPanel(mvcManager, explorePanelT, param, out string panelErr))
                    err = "mapfilters: open explore-panel failed: " + panelErr;
            }
            catch (System.Exception e) { err = "mapfilters: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            try
            {
                object exploreCtl = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                if (exploreCtl == null) { err = "mapfilters: ExplorePanelController not found"; }
                else
                {
                    object navmapCtl = GetPublicProperty(exploreCtl, "NavmapController");
                    if (navmapCtl == null) { err = "mapfilters: NavmapController not found"; }
                    else
                    {
                        object navmapLocationCtl = GetPrivateField(navmapCtl, "navmapLocationController");
                        if (navmapLocationCtl == null) { err = "mapfilters: navmapLocationController not found"; }
                        else
                        {
                            MethodInfo toggleMethod = navmapLocationCtl.GetType().GetMethod("ToggleFilterPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (toggleMethod == null) { err = "mapfilters: ToggleFilterPanel not found"; }
                            else toggleMethod.Invoke(navmapLocationCtl, null);
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "mapfilters: " + (e.InnerException?.Message ?? e.Message); }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("mapfilters");
            m.error = err ?? "shown";
        }

        private static IEnumerator AtlasCapture_sidebar(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_sidebar", ok = true };
            report.actions.Add(m);

            if (mvcManager == null) { m.error = "sidebar: mvcManager is null"; yield break; }

            yield return CloseOpenPanels(mvcManager);

            for (int i = 0; i < 18; i++) yield return null;

            HideChat(mvcManager);
            yield return CaptureShot("sidebar");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_minimap(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_minimap", ok = true };
            report.actions.Add(m);

            yield return CloseOpenPanels(mvcManager);

            string note = null;
            try
            {
                object minimapController = FindControllerByTypeName(mvcManager, "MinimapController");
                if (minimapController == null)
                    note = "minimap(non-gating): MinimapController not registered (capturing bare HUD)";
                else
                {
                    MethodInfo expand = minimapController.GetType().GetMethod(
                        "ExpandMinimap",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (expand == null)
                        note = "minimap(non-gating): ExpandMinimap not found (capturing default state)";
                    else
                        expand.Invoke(minimapController, null);
                }
            }
            catch (System.Exception e) { note = "minimap(non-gating): " + (e.InnerException?.Message ?? e.Message); }

            for (int i = 0; i < 24; i++) yield return null;

            yield return CaptureShot("minimap");
            m.error = note ?? "shown";
        }

        private static IEnumerator AtlasCapture_chat(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_chat", ok = true };
            report.actions.Add(m);

            string err = null;
            try
            {
                if (mvcManager == null)
                {
                    err = "chat: mvcManager is null";
                }
                else
                {

                    MethodInfo closeM = mvcManager.GetType().GetMethod(
                        "CloseAllNonPersistentViews",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (closeM == null)
                        err = "chat: CloseAllNonPersistentViews method not found";
                    else
                        closeM.Invoke(mvcManager, new object[] { System.Threading.CancellationToken.None });
                }
            }
            catch (System.Exception e) { err = "chat: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            yield return ShowChatDefault(mvcManager);

            for (int i = 0; i < 18; i++) yield return null;

            yield return CaptureShot("chat");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_notifications(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_notifications", ok = true };
            report.actions.Add(m);

            string err = null;
            bool opened = false;
            try
            {
                if (mvcManager == null) err = "notifications: mvcManager null";
                else
                {

                    opened = TryShowPanelByName(mvcManager, "DCL.Notifications.NotificationsMenu.NotificationsPanelController", null, out err);
                    if (!opened && err == null) err = "notifications: TryShowPanelByName failed";
                }
            }
            catch (System.Exception e) { err = "notifications: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 8; i++) yield return null;

            const int MAX_POLL = 360;
            bool loadDone = false;
            for (int i = 0; i < MAX_POLL; i++)
            {
                bool spinnerActive = true;
                bool sawState = false;
                try
                {
                    object ctl = FindControllerByTypeName(mvcManager, "NotificationsPanelController");
                    if (ctl != null)
                    {

                        object view = GetMember(ctl, "viewInstance");
                        object spinner = view != null ? GetMember(view, "LoadingSpinner") : null;
                        if (spinner != null)
                        {
                            object active = GetMember(spinner, "activeSelf");
                            if (active is bool b) { spinnerActive = b; sawState = true; }
                        }

                        if (!sawState)
                        {
                            object list = GetPrivateField(ctl, "notifications");
                            object cnt = list != null ? GetMember(list, "Count") : null;
                            if (cnt is int n) { sawState = true; spinnerActive = (n == 0); }
                        }
                    }
                }
                catch {  }

                if (sawState && !spinnerActive) { loadDone = true; break; }
                yield return null;
            }

            if (!loadDone)
                for (int i = 0; i < 60; i++) yield return null;

            for (int i = 0; i < 24; i++) yield return null;
            HideChat(mvcManager);
            yield return CaptureShot("notifications");

            string verifyErr = "no panel key";
            bool shown = lastPanelKey != null && VerifyShown(mvcManager, lastPanelKey, out verifyErr);
            m.error = shown ? "shown" : ("not-shown: " + (verifyErr ?? "no panel key"));
        }

        private static IEnumerator AtlasCapture_connectionstatus(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_connectionstatus", ok = true };
            report.actions.Add(m);

            string err = null;
            object controller = null;
            MethodInfo toggleMethod = null;
            try
            {

                Type busType = FindType("DCL.Chat.Commands.ChatCommandsBus");
                if (busType == null) { err = "connectionstatus: ChatCommandsBus type not found"; }

                if (err == null)
                {
                    PropertyInfo instanceProp = busType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    object busInstance = instanceProp != null ? instanceProp.GetValue(null) : null;
                    if (busInstance == null) { err = "connectionstatus: ChatCommandsBus.Instance is null"; }
                    else
                    {
                        MethodInfo notify = busType.GetMethod("SendConnectionStatusPanelChangedNotification",
                            BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                        if (notify == null) { err = "connectionstatus: SendConnectionStatusPanelChangedNotification(bool) not found"; }
                        else notify.Invoke(busInstance, new object[] { true });
                    }
                }

                if (err == null)
                {
                    Type ctlType = FindType("DCL.UI.ConnectionStatusPanel.ConnectionStatusPanelController");
                    if (ctlType == null) { err = "connectionstatus: ConnectionStatusPanelController type not found"; }
                    else
                    {
                        var found = UnityEngine.Object.FindObjectsByType(ctlType, FindObjectsInactive.Include);
                        controller = (found != null && found.Length > 0) ? found[0] : null;
                        if (controller == null) { err = "connectionstatus: ConnectionStatusPanelController instance not found (plugin not loaded?)"; }
                    }
                }

                if (err == null)
                {
                    Type ctlType = controller.GetType();

                    object go = ctlType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance)?.GetValue(controller);
                    if (go != null)
                    {
                        bool active = (bool)(go.GetType().GetProperty("activeSelf").GetValue(go));
                        if (!active)
                            go.GetType().GetMethod("SetActive", new[] { typeof(bool) }).Invoke(go, new object[] { true });
                    }

                    MethodInfo setEnabled = ctlType.GetMethod("SetPanelEnabled", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                    if (setEnabled != null) setEnabled.Invoke(controller, new object[] { true });

                    Type csEnum = FindType("DCL.UI.ConnectionStatusPanel.ConnectionStatus");
                    Type abEnum = FindType("DCL.Ipfs.AssetBundleRegistryEnum");
                    if (csEnum != null)
                    {
                        object good = Enum.Parse(csEnum, "Good");
                        object excellent = Enum.Parse(csEnum, "Excellent");
                        ctlType.GetMethod("SetSceneStatus", new[] { csEnum })?.Invoke(controller, new[] { good });
                        ctlType.GetMethod("SetSceneRoomStatus", new[] { csEnum })?.Invoke(controller, new[] { excellent });
                        ctlType.GetMethod("SetGlobalRoomStatus", new[] { csEnum })?.Invoke(controller, new[] { good });
                    }
                    if (abEnum != null)
                    {

                        MethodInfo setAb = null;
                        foreach (MethodInfo mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (mi.Name == "SetAssetBundleSceneStatus" && mi.GetParameters().Length == 1) { setAb = mi; break; }
                        if (setAb != null) setAb.Invoke(controller, new[] { Enum.Parse(abEnum, "complete") });
                    }

                    toggleMethod = ctlType.GetMethod("OnToggleButtonClicked", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (toggleMethod != null) toggleMethod.Invoke(controller, null);
                    else
                    {

                        object panelView = GetPrivateField(controller, "panelView");
                        MethodInfo toggle = panelView?.GetType().GetMethod("Toggle", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                        if (toggle != null) toggle.Invoke(panelView, null);
                        else err = "connectionstatus: neither OnToggleButtonClicked nor panelView.Toggle found";
                    }
                }
            }
            catch (System.Exception e) { err = "connectionstatus: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("connectionstatus");

            try
            {
                if (toggleMethod != null && controller != null)
                    toggleMethod.Invoke(controller, null);
                MethodInfo setEnabled = controller?.GetType().GetMethod("SetPanelEnabled", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                if (setEnabled != null) setEnabled.Invoke(controller, new object[] { false });
            }
            catch { }

            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_inputsuggestions(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_inputsuggestions", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            yield return ShowAndFocusChat(mvcManager);

            string err = null;
            object inputSuggestionPanelView = null;

            object customInputField = null;
            try
            {
                object chatCtl = FindControllerByTypeName(mvcManager, "ChatMainSharedAreaController");
                if (chatCtl == null) err = "inputsuggestions: ChatMainSharedAreaController instance not found";

                object view = chatCtl != null ? GetMember(chatCtl, "viewInstance") : null;
                if (err == null && view == null) err = "inputsuggestions: viewInstance null";

                object chatPanelView = view != null ? GetMember(view, "ChatPanelView") : null;
                if (err == null && chatPanelView == null) err = "inputsuggestions: ChatPanelView not found";

                object inputView = chatPanelView != null ? GetMember(chatPanelView, "InputView") : null;
                if (err == null && inputView == null) err = "inputsuggestions: InputView not found";

                if (err == null)
                {
                    inputSuggestionPanelView = GetMember(inputView, "suggestionPanel");
                    if (inputSuggestionPanelView == null) err = "inputsuggestions: suggestionPanel not found";

                    customInputField = GetMember(inputView, "inputField");
                    if (err == null && customInputField == null) err = "inputsuggestions: inputField not found";
                }
            }
            catch (System.Exception e) { err = "inputsuggestions: suggestion-panel lookup failed: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield return CaptureShot("inputsuggestions"); yield break; }

            try
            {
                MethodInfo setTextMethod = customInputField.GetType().GetMethod("SetTextWithoutNotify", BindingFlags.Public | BindingFlags.Instance);
                if (setTextMethod != null)
                    setTextMethod.Invoke(customInputField, new object[] { "@test" });

                object onValueChanged = GetMember(customInputField, "onValueChanged");
                if (onValueChanged != null)
                {
                    MethodInfo invokeMethod = onValueChanged.GetType().GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
                    if (invokeMethod != null)
                        invokeMethod.Invoke(onValueChanged, new object[] { "@test" });
                }
            }
            catch (System.Exception e) { err = "inputsuggestions: input-trigger failed: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield return CaptureShot("inputsuggestions"); yield break; }

            for (int i = 0; i < 10; i++) yield return null;

            string note = "shown";
            try
            {
                object isActive = GetMember(inputSuggestionPanelView, "IsActive");
                if (!(isActive is bool active) || !active)
                    note = "shown (suggestion-panel reported inactive; data-gated @mention candidates may be empty)";
            }
            catch (System.Exception e) { note = "shown (verify-active failed: " + (e.InnerException?.Message ?? e.Message) + ")"; }

            yield return CaptureShot("inputsuggestions");
            m.error = note;
        }

        private static IEnumerator AtlasCapture_chatwindow(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_chatwindow", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            yield return ShowAndFocusChat(mvcManager);

            yield return CaptureShot("chatwindow");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_reactions(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_reactions", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            yield return ShowAndFocusChat(mvcManager);

            string err = null;
            object reactionButtonView = null;
            object reactionButton = null;

            try
            {

                Type viewType = FindType("DCL.Chat.ChatReactions.Views.ChatReactionButtonView");
                if (viewType == null)
                {
                    err = "reactions: ChatReactionButtonView type not found";
                }
                else
                {
                    var found = UnityEngine.Object.FindObjectsByType(viewType, FindObjectsInactive.Include);

                    foreach (var o in found)
                    {
                        if (o is Behaviour beh && beh != null && beh.isActiveAndEnabled) { reactionButtonView = o; break; }
                        if (o is Component comp && comp != null && comp.gameObject.activeInHierarchy) { reactionButtonView = o; break; }
                    }
                    if (reactionButtonView == null && found != null && found.Length > 0)
                        reactionButtonView = found[0];

                    if (reactionButtonView == null)
                        err = "reactions: no ChatReactionButtonView instance in scene (chat not initialized?)";
                    else
                        reactionButton = GetPublicProperty(reactionButtonView, "ReactionButton");
                }
            }
            catch (System.Exception e) { err = "reactions: " + (e.InnerException?.Message ?? e.Message); }

            if (err != null)
            {
                for (int i = 0; i < 18; i++) yield return null;
                yield return CaptureShot("reactions");
                m.error = err + " (captured fallback)";
                yield break;
            }

            try
            {

                object onClick = reactionButton != null ? GetMember(reactionButton, "onClick") : null;
                var ocInvoke = onClick?.GetType().GetMethod("Invoke", System.Type.EmptyTypes);
                if (ocInvoke != null) ocInvoke.Invoke(onClick, null);
                else err = "reactions: ReactionButton.onClick.Invoke() not reachable";
            }
            catch (System.Exception e) { err = "reactions: onClick invoke failed: " + (e.InnerException?.Message ?? e.Message); }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("reactions");
            m.error = err == null ? "shown" : err + " (captured fallback)";
        }

        private static IEnumerator AtlasCapture_chatprofile(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_chatprofile", ok = true };
            report.actions.Add(m);

            string err = null;
            string targetUserId = null;
            bool isOwnProfile = false;
            try
            {
                Type feedType = FindType("DCL.Chat.ChatMessages.ChatMessageFeedView");
                if (feedType == null)
                    err = "chatprofile: ChatMessageFeedView type not found";
                else
                {
                    var feeds = UnityEngine.Object.FindObjectsByType(feedType, FindObjectsInactive.Include);
                    if (feeds == null || feeds.Length == 0)
                        err = "chatprofile: no ChatMessageFeedView instance in scene";
                    else
                    {
                        FieldInfo vmField = feedType.GetField("viewModels", BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var feed in feeds)
                        {
                            object listObj = vmField != null ? vmField.GetValue(feed) : null;
                            var list = listObj as System.Collections.IEnumerable;
                            if (list == null) continue;
                            foreach (var vm in list)
                            {
                                if (vm == null) continue;

                                object msg = GetPublicProperty(vm, "Message");
                                if (msg == null) continue;
                                bool isOwn = false, isSystem = false;
                                object isOwnObj = GetMember(msg, "IsSentByOwnUser");
                                object isSysObj = GetMember(msg, "IsSystemMessage");
                                if (isOwnObj is bool b1) isOwn = b1;
                                if (isSysObj is bool b2) isSystem = b2;
                                if (isOwn || isSystem) continue;
                                string wallet = GetMember(msg, "SenderWalletAddress") as string;
                                if (string.IsNullOrEmpty(wallet))
                                    wallet = GetMember(msg, "SenderWalletId") as string;
                                if (!string.IsNullOrEmpty(wallet))
                                {
                                    targetUserId = wallet;
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(targetUserId)) break;
                        }
                        if (string.IsNullOrEmpty(targetUserId))
                            err = "chatprofile: no other-user message rendered; degrading to own profile";
                    }
                }
            }
            catch (System.Exception e) { err = "chatprofile: scan: " + (e.InnerException?.Message ?? e.Message); }

            object profileTask = null;
            if (string.IsNullOrEmpty(targetUserId))
            {
                isOwnProfile = true;
                try
                {
                    object selfProfile = ReachSelfProfile(dynamicContainer);
                    if (selfProfile != null)
                    {
                        object ownProfile = GetPublicProperty(selfProfile, "OwnProfile");
                        if (ownProfile != null)
                            targetUserId = GetPublicProperty(ownProfile, "UserId") as string;

                        if (string.IsNullOrEmpty(targetUserId))
                        {
                            MethodInfo profileAsync = selfProfile.GetType()
                                .GetMethod("ProfileAsync", BindingFlags.Public | BindingFlags.Instance);
                            if (profileAsync != null)
                                profileTask = profileAsync.Invoke(selfProfile, new object[] { System.Threading.CancellationToken.None });
                        }
                    }
                }
                catch (System.Exception e) { err = (err == null ? "" : err + "; ") + "chatprofile: self-id: " + (e.InnerException?.Message ?? e.Message); }
            }

            if (string.IsNullOrEmpty(targetUserId) && profileTask != null)
            {
                yield return AwaitUniTask(profileTask);
                if (awaitedError == null && awaitedResult != null)
                    targetUserId = GetPublicProperty(awaitedResult, "UserId") as string;
            }

            string openErr = null;
            try
            {
                if (string.IsNullOrEmpty(targetUserId))
                    openErr = "no target userId (no chat author and own id unavailable)";
                else
                {
                    Type viewDepsT = FindType("MVC.ViewDependencies");
                    Type web3AddrT = FindType("DCL.Web3.Web3Address");
                    if (viewDepsT == null) openErr = "ViewDependencies type not found";
                    else if (web3AddrT == null) openErr = "Web3Address type not found";
                    else
                    {

                        object facade = viewDepsT.GetProperty("GlobalUIViews", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                        if (facade == null) openErr = "ViewDependencies.GlobalUIViews unavailable (not initialized)";
                        else
                        {

                            object walletId = System.Activator.CreateInstance(web3AddrT, targetUserId);

                            Type tcsT = FindType("Cysharp.Threading.Tasks.UniTaskCompletionSource");
                            object closeMenuTask = null;
                            if (tcsT != null)
                            {
                                object tcs = System.Activator.CreateInstance(tcsT);
                                closeMenuTask = tcsT.GetProperty("Task", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tcs);
                            }
                            if (closeMenuTask == null)
                                openErr = "could not build never-completing UniTask (UniTaskCompletionSource.Task)";
                            else
                            {
                                MethodInfo showM = facade.GetType().GetMethod("ShowUserProfileContextMenuFromWalletIdAsync",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (showM == null) openErr = "ShowUserProfileContextMenuFromWalletIdAsync not found";
                                else
                                {
                                    ParameterInfo[] ps = showM.GetParameters();
                                    object[] args = new object[ps.Length];

                                    var pos = new UnityEngine.Vector3(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f, 0f);
                                    for (int i = 0; i < ps.Length; i++)
                                    {
                                        switch (ps[i].Name)
                                        {
                                            case "walletId":     args[i] = walletId; break;
                                            case "position":     args[i] = pos; break;
                                            case "offset":       args[i] = UnityEngine.Vector2.zero; break;
                                            case "ct":           args[i] = System.Threading.CancellationToken.None; break;
                                            case "closeMenuTask":args[i] = closeMenuTask; break;
                                            default:             args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : System.Type.Missing; break;
                                        }
                                    }
                                    showM.Invoke(facade, args);
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { openErr = e.InnerException?.Message ?? e.Message; }
            if (openErr != null) err = (err == null ? "chatprofile: open: " + openErr : err + "; open: " + openErr);

            for (int i = 0; i < 60; i++) yield return null;

            yield return CaptureShot("chatprofile");

            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_donations(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_donations", ok = true };
            report.actions.Add(m);

            string err = null;
            object placesApi = null;
            object searchTask = null;

            try
            {

                object explore = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                object placesController = explore != null ? GetMember(explore, "PlacesController") : null;
                object placesResults = placesController != null ? GetMember(placesController, "PlacesResultsController") : null;
                placesApi = placesResults != null ? GetPrivateField(placesResults, "placesAPIService") : null;

                if (placesApi == null) err = "donations: placesAPIService not found";

                if (err == null)
                {
                    MethodInfo searchMethod = null;
                    foreach (MethodInfo mi in placesApi.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        if (mi.Name == "SearchDestinationsAsync") { searchMethod = mi; break; }

                    if (searchMethod == null) err = "donations: SearchDestinationsAsync not found";
                    else
                    {
                        ParameterInfo[] searchParams = searchMethod.GetParameters();
                        object[] searchArgs = new object[searchParams.Length];
                        for (int i = 0; i < searchParams.Length; i++)
                        {
                            if (searchParams[i].ParameterType == typeof(int)) searchArgs[i] = (i == 0) ? 0 : 20;
                            else if (searchParams[i].ParameterType == typeof(System.Threading.CancellationToken)) searchArgs[i] = System.Threading.CancellationToken.None;
                            else searchArgs[i] = searchParams[i].HasDefaultValue ? Type.Missing : null;
                        }
                        searchTask = searchMethod.Invoke(placesApi, searchArgs);
                        if (searchTask == null) err = "donations: SearchDestinationsAsync returned null";
                    }
                }
            }
            catch (System.Exception e) { err = "donations: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield return CaptureShot("donations"); yield break; }

            yield return AwaitUniTask(searchTask);
            if (awaitedError != null) { m.error = "donations: search await failed: " + awaitedError; yield return CaptureShot("donations"); yield break; }

            object param = null;
            try
            {

                string creatorAddress = null;
                Vector2Int baseParcel = Vector2Int.zero;
                object data = GetMember(awaitedResult, "data") ?? GetMember(awaitedResult, "Data");
                if (data is System.Collections.IEnumerable en)
                {
                    foreach (object place in en)
                    {
                        string addr = GetMember(place, "creator_address") as string;
                        if (!string.IsNullOrEmpty(addr))
                        {
                            creatorAddress = addr;
                            object bp = GetMember(place, "base_position_processed");
                            if (bp is Vector2Int v2) baseParcel = v2;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(creatorAddress))
                {
                    err = "donations: no place with creator_address found (data-gated)";
                }
                else
                {
                    Type paramType = FindType("DCL.Donations.UI.DonationsPanelParameter");
                    if (paramType == null) err = "donations: DonationsPanelParameter type not found";
                    else
                    {
                        MethodInfo createMethod = null;
                        foreach (MethodInfo mi in paramType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                            if (mi.Name == "Create" && mi.GetParameters().Length == 2) { createMethod = mi; break; }

                        if (createMethod == null) err = "donations: DonationsPanelParameter.Create not found";
                        else
                        {
                            param = createMethod.Invoke(null, new object[] { creatorAddress, baseParcel });
                            if (param == null) err = "donations: Create returned null";
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "donations: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield return CaptureShot("donations"); yield break; }

            bool opened = TryShowPanelByName(mvcManager, "DCL.Donations.UI.DonationsPanelController", param, out err);
            if (!opened) { m.error = "donations: open failed: " + err; yield return CaptureShot("donations"); yield break; }

            bool loaded = false;
            for (int i = 0; i < 360 && !loaded; i++)
            {
                try
                {
                    object ctl = FindControllerByTypeName(mvcManager, "DonationsPanelController");
                    object view = ctl != null ? GetMember(ctl, "viewInstance") : null;
                    object defView = view != null ? GetMember(view, "donationDefaultView") : null;
                    object skeleton = defView != null ? GetMember(defView, "loadingView") : null;
                    object loadingCg = skeleton != null ? GetMember(skeleton, "loadingCanvasGroup") : null;
                    if (loadingCg != null)
                    {
                        object alphaObj = GetMember(loadingCg, "alpha");
                        if (alphaObj is float a && a < 0.05f) loaded = true;
                    }

                    if (!loaded)
                    {
                        object loadedCg = skeleton != null ? GetMember(skeleton, "loadedCanvasGroup") : null;
                        object loadedAlpha = loadedCg != null ? GetMember(loadedCg, "alpha") : null;
                        if (loadedAlpha is float la && la > 0.9f) loaded = true;
                    }
                }
                catch {  }
                yield return null;
            }

            for (int i = 0; i < (loaded ? 48 : 180); i++) yield return null;
            yield return CaptureShot("donations");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_camera(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_camera", ok = true };
            report.actions.Add(m);

            string err = null;
            try
            {

                object ecsWorld = null;
                object realmController = GetPublicProperty(dynamicContainer, "RealmController");
                if (realmController == null) err = "camera: RealmController not found on dynamicContainer";
                else
                {
                    object globalWorld = GetMember(realmController, "GlobalWorld");
                    if (globalWorld == null) err = "camera: GlobalWorld not found on RealmController";
                    else
                    {
                        ecsWorld = GetMember(globalWorld, "EcsWorld");
                        if (ecsWorld == null) err = "camera: EcsWorld not found on GlobalWorld";
                    }
                }

                if (ecsWorld == null && err != null)
                {
                    Type gwType = FindType("Global.Dynamic.GlobalWorld");
                    var sp = gwType?.GetProperty("ECSWorldInstance", BindingFlags.Public | BindingFlags.Static);
                    object viaStatic = sp?.GetValue(null);
                    if (viaStatic != null) { ecsWorld = viaStatic; err = null; }
                }

                if (err == null && ecsWorld != null)
                {
                    Type worldType = ecsWorld.GetType();

                    Type weType = FindType("DCL.CharacterCamera.WorldExtensions");
                    MethodInfo cacheMethod = weType?.GetMethod("CacheCamera", BindingFlags.Public | BindingFlags.Static);
                    if (cacheMethod == null) err = "camera: WorldExtensions.CacheCamera not found";
                    else
                    {
                        object singleInstanceCamera = cacheMethod.Invoke(null, new[] { ecsWorld });
                        if (singleInstanceCamera == null) err = "camera: CacheCamera returned null";
                        else
                        {

                            object cameraEntity = singleInstanceCamera;
                            foreach (MethodInfo op in singleInstanceCamera.GetType()
                                         .GetMethods(BindingFlags.Public | BindingFlags.Static))
                                if (op.Name == "op_Implicit" && op.ReturnType.Name == "Entity")
                                { cameraEntity = op.Invoke(null, new[] { singleInstanceCamera }); break; }

                            Type requestType = FindType("DCL.InWorldCamera.ToggleInWorldCameraRequest");
                            if (requestType == null) err = "camera: ToggleInWorldCameraRequest type not found";
                            else
                            {
                                object request = Activator.CreateInstance(requestType);
                                requestType.GetField("IsEnable")?.SetValue(request, true);
                                requestType.GetField("Source")?.SetValue(request, "Harness");

                                MethodInfo addGeneric = null;
                                foreach (MethodInfo mi in worldType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    if (mi.Name != "Add" || !mi.IsGenericMethodDefinition
                                        || mi.GetGenericArguments().Length != 1
                                        || mi.GetParameters().Length != 2) continue;
                                    if (mi.GetParameters()[0].ParameterType.Name.StartsWith("Entity")) { addGeneric = mi; break; }
                                }
                                if (addGeneric == null) err = "camera: generic World.Add<T>(in Entity,component) not found";
                                else
                                {
                                    addGeneric.MakeGenericMethod(requestType)
                                              .Invoke(ecsWorld, new object[] { cameraEntity, request });
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "camera: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 30; i++) yield return null;
            yield return CaptureShot("camera");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_reel(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_reel", ok = true };
            report.actions.Add(m);

            string err = null;
            try
            {
                if (mvcManager == null) err = "reel: mvcManager null";
                else if (!TryOpenExplorePanel(mvcManager, "CameraReel", null, out string openErr))
                    err = "reel: open CameraReel section: " + openErr;

            }
            catch (System.Exception e) { err = "reel: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            try
            {
                object explorePanelCtl = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                if (explorePanelCtl != null)
                {
                    object cameraReelController = GetMember(explorePanelCtl, "CameraReelController");
                    if (cameraReelController != null)
                    {
                        MethodInfo activate = cameraReelController.GetType()
                            .GetMethod("Activate", BindingFlags.Public | BindingFlags.Instance);
                        activate?.Invoke(cameraReelController, null);
                    }
                }
            }
            catch (System.Exception e)
            {
                string aErr = e.InnerException?.Message ?? e.Message;
                err = (err == null ? "reel: activate: " + aErr : err + "; activate: " + aErr);
            }

            for (int i = 0; i < 45; i++) yield return null;

            if (lastPanelKey != null) VerifyShown(mvcManager, lastPanelKey, out _);
            yield return CaptureShot("reel");

            m.error = err == null ? "shown" : ("shown; " + err);
        }

        private static IEnumerator AtlasCapture_photo(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_photo", ok = true };
            report.actions.Add(m);

            const string PLACEHOLDER_URL = "https://peer.decentraland.org/content/contents/bafybeietrfx6arffgapt65jkawued7mcsu75uuloodf3drxbvq2pfpggei";

            string err = null;
            Type controllerT = null;
            Type paramT = null;
            Type callerCtxT = null;
            Type compactT = null;
            Type galleryEventBusT = null;
            object photoParam = null;

            try
            {
                controllerT      = FindType("DCL.InWorldCamera.PhotoDetail.PhotoDetailController");
                paramT           = FindType("DCL.InWorldCamera.PhotoDetail.PhotoDetailParameter");
                callerCtxT       = FindType("DCL.InWorldCamera.PhotoDetail.PhotoDetailParameter+CallerContext");
                compactT         = FindType("DCL.InWorldCamera.CameraReelStorageService.Schemas.CameraReelResponseCompact");
                galleryEventBusT = FindType("DCL.InWorldCamera.GalleryEventBus");

                if (controllerT == null || paramT == null || callerCtxT == null || compactT == null)
                    err = "photo: PhotoDetail types not found (skipped:no-types)";
                else
                {

                    object reel = System.Activator.CreateInstance(compactT);
                    compactT.GetField("id").SetValue(reel, "atlas-placeholder-0001");
                    compactT.GetField("url").SetValue(reel, PLACEHOLDER_URL);
                    compactT.GetField("thumbnailUrl").SetValue(reel, PLACEHOLDER_URL);
                    compactT.GetField("isPublic").SetValue(reel, true);

                    compactT.GetField("dateTime").SetValue(reel, "2026-01-01T00:00:00.000Z");

                    Type listT = typeof(System.Collections.Generic.List<>).MakeGenericType(compactT);
                    object allReels = System.Activator.CreateInstance(listT);
                    listT.GetMethod("Add").Invoke(allReels, new[] { reel });

                    object galleryEventBus = galleryEventBusT != null ? System.Activator.CreateInstance(galleryEventBusT) : null;

                    object cameraReelCtx = System.Enum.Parse(callerCtxT, "CameraReel");
                    ConstructorInfo ctor = paramT.GetConstructor(new[]
                    {
                        listT, typeof(int), typeof(bool), callerCtxT,
                        typeof(System.Action<>).MakeGenericType(compactT),
                        typeof(System.Action<>).MakeGenericType(compactT),
                        galleryEventBusT
                    });
                    if (ctor == null) err = "photo: PhotoDetailParameter ctor not found (skipped:no-ctor)";
                    else

                        photoParam = ctor.Invoke(new object[] { allReels, 0, true, cameraReelCtx, null, null, galleryEventBus });
                }
            }
            catch (System.Exception e) { err = "photo: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            if (!TryShowPanel(mvcManager, controllerT, photoParam, out string showErr))
            { m.error = "photo: TryShowPanel failed: " + showErr + " (skipped:show-error)"; yield break; }

            for (int i = 0; i < 300; i++) yield return null;
            VerifyShown(mvcManager, lastPanelKey, out _);
            yield return CaptureShot("photo");
            m.error = "shown";
        }

private static IEnumerator AtlasCapture_gifting(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
{
    var m = new PhaseMarker { label = "atlas_gifting", ok = true };
    report.actions.Add(m);

    string err = null;
    object giftParams = null;
    try
    {

        Type paramType = FindType("DCL.Backpack.Gifting.Views.GiftSelectionParams");
        if (paramType == null) { err = "gifting: GiftSelectionParams type not found"; }
        else
        {
            ConstructorInfo paramCtor = paramType.GetConstructor(new[] { typeof(string), typeof(string) });
            if (paramCtor == null) { err = "gifting: GiftSelectionParams(string,string) ctor not found"; }
            else
                giftParams = paramCtor.Invoke(new object[]
                {
                    "0x0000000000000000000000000000000000000000",
                    "Recipient",
                });
        }
    }
    catch (System.Exception e) { err = "gifting: " + (e.InnerException?.Message ?? e.Message); }
    if (err != null) { m.error = err; yield break; }

    if (!TryShowPanelByName(mvcManager, "DCL.Backpack.Gifting.Presenters.GiftSelectionController", giftParams, out string showErr))
    { m.error = "gifting: " + showErr; yield break; }

    for (int i = 0; i < 18; i++) yield return null;

    bool loaded = false;
    for (int i = 0; i < 360 && !loaded; i++)
    {
        try
        {
            object ctl = FindControllerByTypeName(mvcManager, "GiftSelectionController");
            if (ctl != null)
            {

                object tabsManager = GetPrivateField(ctl, "tabsManager");
                object active = tabsManager != null ? GetMember(tabsManager, "ActivePresenter") : null;
                object countObj = active != null ? GetMember(active, "CurrentItemCount") : null;
                if (countObj is int cnt && cnt > 0) loaded = true;

                if (!loaded)
                {
                    object viewInstance = GetMember(ctl, "viewInstance");
                    object progress = viewInstance != null ? GetMember(viewInstance, "ProgressContainer") : null;
                    object spinnerActive = progress != null ? GetMember(progress, "activeInHierarchy") : null;

                    if (i > 30 && spinnerActive is bool sa && sa == false) loaded = true;
                }
            }
        }
        catch {  }

        if (!loaded) yield return null;
    }

    for (int i = 0; i < 12; i++) yield return null;
    yield return CaptureShot("gifting");
    m.error = "shown";
}

        private static IEnumerator AtlasCapture_creditsunlocked(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_creditsunlocked", ok = true };
            report.actions.Add(m);

            string err = null;
            object param = null;
            try
            {

                Type paramsType = FindType("DCL.MarketplaceCredits.CreditsUnlockedController+Params");
                if (paramsType == null) { err = "creditsunlocked: Params type not found"; }
                else
                {
                    ConstructorInfo paramsCtor = paramsType.GetConstructor(new[] { typeof(float) });
                    if (paramsCtor == null) { err = "creditsunlocked: Params(float) constructor not found"; }
                    else param = paramsCtor.Invoke(new object[] { 250f });
                }
            }
            catch (System.Exception e) { err = "creditsunlocked: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            yield return PreShowSettle(mvcManager, "CreditsUnlockedController");

            if (!TryShowPanelByName(mvcManager, "DCL.MarketplaceCredits.CreditsUnlockedController", param, out err))
            { m.error = "creditsunlocked: " + err; yield break; }

            for (int i = 0; i < 75; i++) yield return null;
            yield return CaptureShot("creditsunlocked");

            if (!VerifyShown(mvcManager, lastPanelKey, out string rerr)) m.error = "not-shown: " + rerr;
            else m.error = "shown";
        }

        private static IEnumerator AtlasCapture_creditsstates(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_creditsstates", ok = true };
            report.actions.Add(m);

            string err = null;
            object param = null;
            try
            {

                Type paramsType = FindType("DCL.MarketplaceCredits.MarketplaceCreditsMenuController+Params");
                if (paramsType == null) err = "creditsstates: Params type not found";
                else
                {
                    ConstructorInfo paramsCtor = paramsType.GetConstructor(new[] { typeof(bool) });
                    if (paramsCtor == null) err = "creditsstates: Params(bool) constructor not found";
                    else param = paramsCtor.Invoke(new object[] { false });
                }

                if (err == null && !TryShowPanelByName(mvcManager, "DCL.MarketplaceCredits.MarketplaceCreditsMenuController", param, out string showErr))
                    err = "creditsstates: open panel failed: " + showErr;
            }
            catch (System.Exception e) { err = "creditsstates: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 24; i++) yield return null;

            try
            {
                object ctl = FindControllerByTypeName(mvcManager, "MarketplaceCreditsMenuController");
                Type sectionEnum = FindType("DCL.MarketplaceCredits.MarketplaceCreditsSection");
                Type respType = FindType("DCL.MarketplaceCredits.CreditsProgramProgressResponse");
                Type goalType = FindType("DCL.MarketplaceCredits.GoalData");
                Type goalProgType = FindType("DCL.MarketplaceCredits.GoalProgressData");
                Type weekType = FindType("DCL.MarketplaceCredits.Week");
                Type creditsType = FindType("DCL.MarketplaceCredits.CreditsData");
                Type userType = FindType("DCL.MarketplaceCredits.UserData");

                if (ctl != null && sectionEnum != null && respType != null && goalType != null
                    && goalProgType != null && weekType != null && creditsType != null && userType != null)
                {

                    object MakeGoal(string title, string desc, uint completed, uint total, float reward, bool claimed)
                    {
                        object prog = System.Activator.CreateInstance(goalProgType);
                        goalProgType.GetField("totalSteps").SetValue(prog, total);
                        goalProgType.GetField("completedSteps").SetValue(prog, completed);

                        object goal = System.Activator.CreateInstance(goalType);
                        goalType.GetField("title").SetValue(goal, title);
                        goalType.GetField("description").SetValue(goal, desc);
                        goalType.GetField("thumbnail").SetValue(goal, "");
                        goalType.GetField("progress").SetValue(goal, prog);
                        goalType.GetField("reward").SetValue(goal, reward);
                        goalType.GetField("isClaimed").SetValue(goal, claimed);
                        return goal;
                    }

                    Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(goalType);
                    object goals = System.Activator.CreateInstance(listType);
                    MethodInfo add = listType.GetMethod("Add");
                    add.Invoke(goals, new[] { MakeGoal("Walk around Genesis City", "Explore the world", 2u, 5u, 100f, false) });
                    add.Invoke(goals, new[] { MakeGoal("Customize your avatar", "Change your look", 1u, 1u, 50f, true) });

                    object week = System.Activator.CreateInstance(weekType);
                    weekType.GetField("weekNumber").SetValue(week, 1);
                    weekType.GetField("timeLeft").SetValue(week, (uint)259200);
                    weekType.GetField("startDate").SetValue(week, "");
                    weekType.GetField("endDate").SetValue(week, "");
                    weekType.GetField("secondsRemaining").SetValue(week, (uint)259200);

                    object credits = System.Activator.CreateInstance(creditsType);
                    creditsType.GetField("available").SetValue(credits, 150f);
                    creditsType.GetField("expiresIn").SetValue(credits, (uint)30);
                    creditsType.GetField("isBlockedForClaiming").SetValue(credits, false);

                    object user = System.Activator.CreateInstance(userType);
                    userType.GetField("email").SetValue(user, "user@example.com");
                    userType.GetField("isEmailConfirmed").SetValue(user, true);
                    userType.GetField("hasStartedProgram").SetValue(user, true);

                    object resp = System.Activator.CreateInstance(respType);
                    respType.GetField("currentWeek").SetValue(resp, week);
                    respType.GetField("credits").SetValue(resp, credits);
                    respType.GetField("user").SetValue(resp, user);
                    respType.GetField("goals").SetValue(resp, goals);

                    object goalsSection = System.Enum.Parse(sectionEnum, "GOALS_OF_THE_WEEK");
                    MethodInfo openSection = ctl.GetType().GetMethod("OpenSection",
                        BindingFlags.Public | BindingFlags.Instance, null, new[] { sectionEnum }, null);
                    if (openSection != null)
                        openSection.Invoke(ctl, new[] { goalsSection });

                    object goalsSub = GetPrivateField(ctl, "marketplaceCreditsGoalsOfTheWeekSubController");
                    if (goalsSub != null)
                        TryInvoke(goalsSub, "Setup", new[] { resp }, out string _);
                }
            }
            catch (System.Exception) {  }

            for (int i = 0; i < 24; i++) yield return null;

            HideChat(mvcManager);
            yield return CaptureShot("creditsstates");

            if (!VerifyShown(mvcManager, lastPanelKey, out string rerr)) m.error = "not-shown: " + rerr;
            else m.error = "shown";
        }

        private static IEnumerator AtlasCapture_emoji(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_emoji", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            yield return ShowAndFocusChat(mvcManager);

            string err = null;
            object emojiView = null;
            try
            {
                Type viewT = FindType("DCL.Emoji.EmojiPanelView");
                if (viewT == null)
                {
                    err = "emoji: type DCL.Emoji.EmojiPanelView not found";
                }
                else
                {
                    foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(viewT, FindObjectsInactive.Include))
                    {
                        emojiView = o;
                        break;
                    }

                    if (emojiView == null)
                    {
                        err = "emoji: no EmojiPanelView instance in scene";
                    }
                    else
                    {

                        MethodInfo setVisible = emojiView.GetType().GetMethod("SetVisible",
                            BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);

                        if (setVisible == null)
                            err = "emoji: SetVisible(bool) not found on EmojiPanelView";
                        else
                            setVisible.Invoke(emojiView, new object[] { true });
                    }
                }
            }
            catch (System.Exception e) { err = "emoji: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            string visNote = null;
            try
            {
                object isVisible = GetPublicProperty(emojiView, "IsVisible");
                if (isVisible is bool visible && !visible)
                    visNote = "shown but EmojiPanelView.IsVisible == false";
            }
            catch (System.Exception e) { visNote = "verify: " + (e.InnerException?.Message ?? e.Message); }

            yield return CaptureShot("emoji");
            m.error = visNote ?? "shown";
        }

        private static IEnumerator AtlasCapture_teleportprompt(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_teleportprompt", ok = true };
            report.actions.Add(m);

            string err = null;
            bool opened = false;
            try
            {
                if (mvcManager == null) { err = "teleportprompt: mvcManager is null"; }
                else
                {

                    Type paramT = FindType("DCL.TeleportPrompt.TeleportPromptController+Params");
                    if (paramT == null) { err = "teleportprompt: Params type not found"; }
                    else
                    {
                        object param = Activator.CreateInstance(paramT, new Vector2Int(74, -9));

                        opened = TryShowPanelByName(mvcManager, "DCL.TeleportPrompt.TeleportPromptController", param, out err);
                    }
                }
            }
            catch (System.Exception e) { err = "teleportprompt: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = "not-shown: " + err; yield break; }

            for (int i = 0; i < 36; i++) yield return null;
            yield return CaptureShot("teleportprompt");

            string rerr = null;
            try { if (!VerifyShown(mvcManager, lastPanelKey, out rerr)) rerr = "not-shown: " + rerr; else rerr = null; }
            catch (System.Exception e) { rerr = "verify-failed: " + (e.InnerException?.Message ?? e.Message); }
            m.error = rerr ?? "shown";
        }

        private static IEnumerator AtlasCapture_nftprompt(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_nftprompt", ok = true };
            report.actions.Add(m);

            string err = null;
            Type panelKey = null;
            try
            {

                Type paramT = FindType("DCL.NftPrompt.NftPromptController+Params");
                if (paramT == null) { err = "nftprompt: Params type not found"; }

                Type controllerT = err == null ? FindType("DCL.NftPrompt.NftPromptController") : null;
                if (err == null && controllerT == null) { err = "nftprompt: NftPromptController type not found"; }

                if (err == null)
                {
                    object param = Activator.CreateInstance(paramT,
                        "ethereum",
                        "0x06012c8cf97bead5deae237070f9587f8e7a266d",
                        "1540722");

                    MethodInfo issue = null;
                    foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                        if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 1) { issue = mi; break; }
                    if (issue == null) { err = "nftprompt: IssueCommand(1-arg) not found"; }

                    object command = issue != null ? issue.Invoke(null, new[] { param }) : null;
                    if (err == null && command == null) { err = "nftprompt: IssueCommand returned null"; }

                    if (err == null)
                    {

                        MethodInfo showAsync = null;
                        foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                        if (showAsync == null) { err = "nftprompt: ShowAsync not found"; }

                        if (err == null)
                        {
                            Type[] genArgs = command.GetType().GetGenericArguments();

                            showAsync.MakeGenericMethod(genArgs)
                                     .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });

                            Type ifaceOpen = FindType("MVC.IController`2");
                            panelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                        }
                    }
                }
            }
            catch (Exception e) { err = "nftprompt: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 30; i++) yield return null;

            bool loaded = false;
            bool errored = false;
            for (int i = 0; i < 300 && !loaded && !errored; i++)
            {
                try
                {
                    object ctl = FindControllerByTypeName(mvcManager, "NftPromptController");
                    object view = ctl != null ? GetMember(ctl, "viewInstance") : null;
                    if (view != null)
                    {
                        object nftContent = GetMember(view, "NftContent");
                        var contentGo = nftContent as UnityEngine.GameObject;
                        if (contentGo != null && contentGo.activeSelf) loaded = true;

                        object errFeedback = GetMember(view, "MainErrorFeedbackContent");
                        var errGo = errFeedback as UnityEngine.GameObject;
                        if (errGo != null && errGo.activeSelf) errored = true;
                    }
                }
                catch {  }

                yield return null;
            }

            int settleFrames = loaded ? 180 : 60;
            for (int i = 0; i < settleFrames; i++) yield return null;

            if (panelKey != null && !VerifyShown(mvcManager, panelKey, out string verifyErr))
                m.error = "nftprompt: " + verifyErr;

            yield return CaptureShot("nftprompt");

            if (m.error == null)
                m.error = loaded ? "shown" : (errored ? "shown: nft-fetch-failed (error feedback)" : "shown: nft-fetch-timeout");
        }

        private static IEnumerator AtlasCapture_reward(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_reward", ok = true };
            report.actions.Add(m);

            string err = null;
            Type panelKey = null;
            try
            {

                Type paramT = FindType("DCL.RewardPanel.RewardPanelParameter");
                if (paramT == null) { err = "reward: RewardPanelParameter type not found"; }

                Type controllerT = err == null ? FindType("DCL.RewardPanel.RewardPanelController") : null;
                if (err == null && controllerT == null) { err = "reward: RewardPanelController type not found"; }

                if (err == null)
                {

                    object param = Activator.CreateInstance(paramT,
                        "https://peer.decentraland.org/content/contents/bafybeihfypqzqr7l2v3fvhx25gw32qxc4rkmmulccm2ij5p7quhzqxypy",
                        "Test Reward Wearable",
                        "epic",
                        "eyewear");

                    MethodInfo issue = null;
                    foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                        if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 1) { issue = mi; break; }
                    if (issue == null) { err = "reward: IssueCommand(1-arg) not found"; }

                    object command = issue != null ? issue.Invoke(null, new[] { param }) : null;
                    if (err == null && command == null) { err = "reward: IssueCommand returned null"; }

                    if (err == null)
                    {

                        MethodInfo showAsync = null;
                        foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                        if (showAsync == null) { err = "reward: ShowAsync not found"; }

                        if (err == null)
                        {
                            Type[] genArgs = command.GetType().GetGenericArguments();

                            showAsync.MakeGenericMethod(genArgs)
                                     .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });

                            Type ifaceOpen = FindType("MVC.IController`2");
                            panelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                        }
                    }
                }
            }
            catch (Exception e) { err = "reward: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 90; i++) yield return null;

            if (panelKey != null && !VerifyShown(mvcManager, panelKey, out string verifyErr))
                m.error = "reward: " + verifyErr;

            HideRewardsPopup();
            HideChat(mvcManager);

            yield return CaptureShot("reward");

            if (m.error == null)
                m.error = "shown";
        }

        private static IEnumerator AtlasCapture_privateworlds(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_privateworlds", ok = true };
            report.actions.Add(m);

            string err = null;
            object popupParams = null;
            try
            {

                Type paramsT = FindType("DCL.PrivateWorlds.UI.PrivateWorldPopupParams");
                Type modeT = FindType("DCL.PrivateWorlds.UI.PrivateWorldPopupMode");
                if (paramsT == null) err = "privateworlds: PrivateWorldPopupParams type not found";
                else if (modeT == null) err = "privateworlds: PrivateWorldPopupMode enum not found";
                else
                {
                    object passwordRequired = System.Enum.Parse(modeT, "PasswordRequired");
                    ConstructorInfo ctor = paramsT.GetConstructor(new[] { typeof(string), modeT, typeof(string) });
                    if (ctor != null)
                        popupParams = ctor.Invoke(new object[] { "private-world", passwordRequired, string.Empty });
                    else
                        popupParams = System.Activator.CreateInstance(paramsT);
                }
            }
            catch (System.Exception e) { err = "privateworlds: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            string showErr;
            bool opened = TryShowPanelByName(mvcManager, "DCL.PrivateWorlds.UI.PrivateWorldPopupController", popupParams, out showErr);
            if (!opened)
            {
                m.error = "privateworlds: show-failed (" + showErr + ")";
                yield break;
            }

            for (int i = 0; i < 18; i++) yield return null;

            string verifyErr = null;
            if (lastPanelKey != null && !VerifyShown(mvcManager, lastPanelKey, out verifyErr))
            {
                m.error = "privateworlds: not-shown (" + verifyErr + ")";
                yield return CaptureShot("privateworlds");
                yield break;
            }

            yield return CaptureShot("privateworlds");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_smartwearables(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_smartwearables", ok = true };
            report.actions.Add(m);

            string err = null;
            object param = null;
            string controllerFull = "Runtime.Wearables.SmartWearableAuthorizationPopupController";
            try
            {
                Type wearableConcreteT = FindType("DCL.AvatarRendering.Wearables.Components.Wearable");
                Type wearableIfaceT    = FindType("DCL.AvatarRendering.Wearables.Components.IWearable");
                Type dtoT              = FindType("DCL.AvatarRendering.Wearables.Helpers.WearableDTO");
                Type metaT             = FindType("DCL.AvatarRendering.Wearables.Helpers.WearableDTO+WearableMetadataDto");
                Type slrOpenT          = FindType("ECS.StreamableLoading.Common.Components.StreamableLoadingResult`1");
                Type reprT             = FindType("DCL.AvatarRendering.Loading.DTO.AvatarAttachmentDTO+Representation");
                Type paramT            = FindType(controllerFull + "+Params");
                Type csOpenT           = FindType("Cysharp.Threading.Tasks.UniTaskCompletionSource`1");

                if (wearableConcreteT == null) err = "smartwearables: Wearable concrete type not found";
                else if (wearableIfaceT == null) err = "smartwearables: IWearable type not found";
                else if (dtoT == null) err = "smartwearables: WearableDTO type not found";
                else if (metaT == null) err = "smartwearables: WearableMetadataDto type not found";
                else if (slrOpenT == null) err = "smartwearables: StreamableLoadingResult`1 not found";
                else if (reprT == null) err = "smartwearables: Representation type not found";
                else if (paramT == null) err = "smartwearables: Params type not found";
                else if (csOpenT == null) err = "smartwearables: UniTaskCompletionSource`1 not found";
                else
                {

                    object metadata = Activator.CreateInstance(metaT);
                    metaT.GetField("id").SetValue(metadata, "urn:decentraland:matic:collections-v2:smart:atlas-preview");
                    metaT.GetField("rarity").SetValue(metadata, "epic");

                    foreach (System.Reflection.FieldInfo fi in metaT.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        if (fi.Name == "name" && fi.DeclaringType.Name == "MetadataBase") fi.SetValue(metadata, "Smart Wearable");
                    metaT.GetField("name").SetValue(metadata, "Smart Wearable");
                    metaT.GetField("thumbnail").SetValue(metadata, "preview-thumbnail");
                    metaT.GetField("description").SetValue(metadata, "Atlas preview smart wearable");

                    object data = metaT.GetField("data").GetValue(metadata);
                    Type dataT = data.GetType();
                    foreach (System.Reflection.FieldInfo fi in dataT.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (fi.Name == "category") fi.SetValue(data, "upper_body");
                        else if (fi.Name == "representations") fi.SetValue(data, System.Array.CreateInstance(reprT, 0));
                    }

                    object dto = Activator.CreateInstance(dtoT);
                    for (Type t = dtoT; t != null; t = t.BaseType)
                    {
                        System.Reflection.FieldInfo fMeta = t.GetField("metadata",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (fMeta != null) { fMeta.SetValue(dto, metadata); break; }
                    }
                    for (Type t = dtoT; t != null; t = t.BaseType)
                    {
                        System.Reflection.FieldInfo fId = t.GetField("id",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (fId != null) { fId.SetValue(dto, "urn:decentraland:matic:collections-v2:smart:atlas-preview"); break; }
                    }
                    for (Type t = dtoT; t != null; t = t.BaseType)
                    {
                        System.Reflection.FieldInfo fThumb = t.GetField("thumbnail",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (fThumb != null) { fThumb.SetValue(dto, "preview-thumbnail"); break; }
                    }

                    Type slrT = slrOpenT.MakeGenericType(dtoT);
                    object resolvedResult = Activator.CreateInstance(slrT, new object[] { dto });

                    ConstructorInfo wctor = wearableConcreteT.GetConstructor(
                        BindingFlags.Public | BindingFlags.Instance, null, new[] { slrT }, null);
                    object wearable = wctor != null
                        ? wctor.Invoke(new object[] { resolvedResult })
                        : null;
                    if (wearable == null) err = "smartwearables: Wearable(StreamableLoadingResult) ctor not found";

                    if (err == null)
                    {

                        Type csBool = csOpenT.MakeGenericType(typeof(bool));
                        object completionSource = Activator.CreateInstance(csBool);
                        ConstructorInfo paramCtor = paramT.GetConstructor(
                            BindingFlags.Public | BindingFlags.Instance, null,
                            new[] { wearableIfaceT, csBool }, null);
                        if (paramCtor == null) err = "smartwearables: Params(IWearable, UniTaskCompletionSource<bool>) ctor not found";
                        else param = paramCtor.Invoke(new object[] { wearable, completionSource });
                    }
                }
            }
            catch (System.Exception e) { err = "smartwearables: " + (e.InnerException?.Message ?? e.Message); }

            bool shown = false;
            if (err == null && param != null && mvcManager != null)
            {
                shown = TryShowPanelByName(mvcManager, controllerFull, param, out string uerr);
                if (!shown) err = "smartwearables: show-failed: " + uerr;
            }
            else if (err == null) err = "smartwearables: param or mvcManager null";

            for (int i = 0; i < 18; i++) yield return null;

            HideRewardsPopup();
            HideChat(mvcManager);
            yield return CaptureShot("smartwearables");

            m.error = shown ? "shown" : (err ?? "not shown (unknown reason)");
        }

        private static IEnumerator AtlasCapture_errorpopup(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_errorpopup", ok = true };
            report.actions.Add(m);

            string err = null;
            object inputObj = null;
            try
            {
                Type dataT = FindType("DCL.UI.ErrorPopup.ErrorPopupData");
                if (dataT == null)
                    err = "errorpopup: type not found (DCL.UI.ErrorPopup.ErrorPopupData)";
                else
                {

                    MethodInfo fromDesc = dataT.GetMethod("FromDescription",
                        BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string) }, null);
                    if (fromDesc == null)
                        err = "errorpopup: ErrorPopupData.FromDescription(string) not found";
                    else
                        inputObj = fromDesc.Invoke(null, new object[]
                        {
                            "Something went wrong while loading Decentraland. Please try again."
                        });
                }
            }
            catch (System.Exception e) { err = "errorpopup: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            if (!TryShowPanelByName(mvcManager, "DCL.UI.ErrorPopup.ErrorPopupController", inputObj, out err))
            { m.error = "errorpopup: " + err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            HideRewardsPopup();
            HideChat(mvcManager);
            yield return CaptureShot("errorpopup");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_login(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_login", ok = true };
            report.actions.Add(m);

            string err = null;
            object loginView = null;
            try
            {
                object authCtl = FindControllerByTypeName(mvcManager, "AuthenticationScreenController");
                object view = authCtl != null ? GetMember(authCtl, "viewInstance") : null;
                if (view == null) { err = "login: skipped:auth-view-null (only reachable in auth mode pre-world)"; }
                else
                {
                    loginView = GetMember(view, "LoginSelectionAuthView");
                    if (loginView == null) err = "login: LoginSelectionAuthView not found";
                    else
                    {
                        var showM = loginView.GetType().GetMethod("Show", BindingFlags.Public | BindingFlags.Instance,
                            null, new[] { typeof(int), typeof(bool) }, null);
                        if (showM == null) err = "login: Show(int,bool) not found";

                        else showM.Invoke(loginView, new object[] { UnityEngine.Animator.StringToHash("In"), false });
                    }
                }
            }
            catch (System.Exception e) { err = "login: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 24; i++) yield return null;

            try
            {
                object av = GetMember(FindControllerByTypeName(mvcManager, "AuthenticationScreenController"), "viewInstance");
                foreach (string sib in new[] { "LobbyForExistingAccountAuthView", "LobbyForNewAccountAuthView", "ProfileFetchingAuthView", "VerificationDappAuthView", "VerificationOTPAuthView" })
                {
                    object sv = av != null ? GetMember(av, sib) : null;
                    if (sv == null) continue;
                    var hideM = sv.GetType().GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                    hideM?.Invoke(sv, null);
                    object go = GetMember(sv, "gameObject");
                    var sa = go?.GetType().GetMethod("SetActive", new[] { typeof(bool) });
                    sa?.Invoke(go, new object[] { false });
                }
            }
            catch { }
            yield return CaptureShot("login");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_loading(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_loading", ok = true };
            report.actions.Add(m);

            string err = null;
            object command = null;
            Type viewType = null;
            Type paramsType = null;

            try
            {

                Type reportType = FindType("DCL.Utilities.AsyncLoadProcessReport");
                if (reportType == null) { err = "loading: AsyncLoadProcessReport type not found"; }
                else
                {
                    System.Reflection.MethodInfo createMethod = reportType.GetMethod("Create",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new[] { typeof(System.Threading.CancellationToken) }, null);
                    if (createMethod == null) { err = "loading: AsyncLoadProcessReport.Create(CancellationToken) not found"; }
                    else
                    {
                        object loadReport = createMethod.Invoke(null, new object[] { System.Threading.CancellationToken.None });
                        if (loadReport == null) { err = "loading: AsyncLoadProcessReport.Create returned null"; }
                        else
                        {

                            Type controllerType = FindType("DCL.SceneLoadingScreens.SceneLoadingScreenController");
                            if (controllerType == null) { err = "loading: SceneLoadingScreenController type not found"; }
                            else
                            {
                                paramsType = controllerType.GetNestedType("Params", System.Reflection.BindingFlags.Public);
                                if (paramsType == null) { err = "loading: SceneLoadingScreenController+Params type not found"; }
                                else
                                {

                                    System.Reflection.ConstructorInfo paramsCtor = paramsType.GetConstructor(new[] { reportType });
                                    if (paramsCtor == null) { err = "loading: Params(AsyncLoadProcessReport) constructor not found"; }
                                    else
                                    {
                                        object paramsInstance = paramsCtor.Invoke(new[] { loadReport });

                                        System.Reflection.MethodInfo issueCommand = controllerType.GetMethod("IssueCommand",
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy,
                                            null, new[] { paramsType }, null);
                                        if (issueCommand == null) { err = "loading: IssueCommand(Params) not found"; }
                                        else
                                        {
                                            command = issueCommand.Invoke(null, new[] { paramsInstance });
                                            if (command == null) { err = "loading: IssueCommand returned null"; }
                                            else
                                            {

                                                viewType = FindType("DCL.SceneLoadingScreens.SceneLoadingScreenView");
                                                if (viewType == null) { err = "loading: SceneLoadingScreenView type not found"; command = null; }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (err == null && command != null)
                {
                    System.Reflection.MethodInfo showAsync = null;
                    foreach (System.Reflection.MethodInfo mi in mvcManager.GetType().GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }

                    if (showAsync == null) { err = "loading: ShowAsync not found on MvcManager"; }
                    else
                    {
                        showAsync.MakeGenericMethod(viewType, paramsType)
                                 .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });

                        Type ifaceOpen = FindType("MVC.IController`2");
                        lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(viewType, paramsType) : null;
                    }
                }
            }
            catch (System.Exception e) { err = "loading: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 30; i++) yield return null;

            if (lastPanelKey != null && !VerifyShown(mvcManager, lastPanelKey, out string verifyErr))
            {

                m.error = "not-shown: " + verifyErr;
                yield return CaptureShot("loading");
                yield break;
            }

            yield return CaptureShot("loading");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_lobbynew(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_lobbynew", ok = true };
            report.actions.Add(m);

            string err = null;
            object lobbyView = null;

            try
            {
                object authCtl = FindControllerByTypeName(mvcManager, "AuthenticationScreenController");
                if (authCtl == null) { err = "lobbynew: not-found:authCtl (auth screen unreachable in-world; auth-capture mode only)"; }
                else
                {

                    object viewInstance = GetMember(authCtl, "viewInstance");
                    if (viewInstance == null) err = "lobbynew: not-shown:viewInstance-null (auth view not yet instantiated)";
                    else
                    {

                        lobbyView = GetMember(viewInstance, "LobbyForNewAccountAuthView");
                        if (lobbyView == null) err = "lobbynew: not-found:LobbyForNewAccountAuthView";
                    }
                }

                if (lobbyView != null)
                {

                    System.Reflection.MethodInfo showM = null;
                    foreach (System.Reflection.MethodInfo mi in lobbyView.GetType().GetMethods(
                                 System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (mi.Name == "Show" && mi.GetParameters().Length == 0) { showM = mi; break; }
                    }
                    if (showM == null) err = "lobbynew: not-found:Show()-method";
                    else
                    {
                        showM.Invoke(lobbyView, null);

                        try
                        {
                            System.Reflection.MethodInfo closeDropdownM = lobbyView.GetType().GetMethod(
                                "SetBodyTypeDropdownOpen",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                null, new[] { typeof(bool) }, null);
                            if (closeDropdownM != null) closeDropdownM.Invoke(lobbyView, new object[] { false });

                            System.Reflection.MethodInfo bodyTypeUIM = lobbyView.GetType().GetMethod(
                                "UpdateBodyTypeUI",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                null, new[] { typeof(bool) }, null);
                            if (bodyTypeUIM != null) bodyTypeUIM.Invoke(lobbyView, new object[] { true });
                        }
                        catch {  }
                    }
                }
            }
            catch (System.Exception e) { err = "lobbynew: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("lobbynew");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_otp(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_otp", ok = true };
            report.actions.Add(m);

            string err = null;
            object otpView = null;

            try
            {
                object authCtl = FindControllerByTypeName(mvcManager, "AuthenticationScreenController");
                if (authCtl == null) { err = "otp: skipped:authCtl-not-found (pre-in-world auth controller not registered)"; }
                else
                {

                    object view = GetMember(authCtl, "viewInstance");
                    if (view == null) err = "otp: skipped:view-instance-null (auth view not instantiated yet)";
                    else
                    {

                        try
                        {
                            object loginView = GetMember(view, "LoginSelectionAuthView");
                            if (loginView != null)
                            {
                                var hideM = loginView.GetType().GetMethod("Hide",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                    null, System.Type.EmptyTypes, null);
                                hideM?.Invoke(loginView, null);
                            }
                        }
                        catch {  }

                        otpView = GetMember(view, "VerificationOTPAuthView");
                        if (otpView == null) err = "otp: skipped:VerificationOTPAuthView-null";
                        else
                        {

                            var showM = otpView.GetType().GetMethod("Show",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                null, new[] { typeof(string) }, null);
                            if (showM == null) err = "otp: skipped:Show(string)-not-found";
                            else showM.Invoke(otpView, new object[] { "your@email.com" });
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "otp: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 24; i++) yield return null;
            yield return CaptureShot("otp");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_verify(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_verify", ok = true };
            report.actions.Add(m);

            string err = null;
            object dappView = null;
            System.Reflection.MethodInfo showMethod = null;
            object[] showArgs = null;

            try
            {
                object authCtl = FindControllerByTypeName(mvcManager, "AuthenticationScreenController");
                if (authCtl == null) { err = "verify: skipped:authCtl-not-found (auth FSM only exists pre-world)"; }
                else
                {

                    object viewInstance = GetMember(authCtl, "viewInstance");
                    if (viewInstance == null) { err = "verify: skipped:viewInstance-null (auth view not instantiated)"; }
                    else
                    {

                        dappView = GetMember(viewInstance, "VerificationDappAuthView");
                        if (dappView == null) { err = "verify: skipped:VerificationDappAuthView-not-found"; }
                        else
                        {

                            showMethod = dappView.GetType().GetMethod(
                                "Show",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                null,
                                new Type[] { typeof(int), typeof(System.DateTime) },
                                null);
                            if (showMethod == null) { err = "verify: skipped:Show(int,DateTime)-not-found"; }
                            else
                            {

                                showArgs = new object[] { 123456, System.DateTime.UtcNow.AddMinutes(5) };
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "verify: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            try { showMethod.Invoke(dappView, showArgs); }
            catch (System.Exception e) { m.error = "verify: show-invoke-failed: " + (e.InnerException?.Message ?? e.Message); }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("verify");
            if (m.error == null) m.error = "shown";
        }

        private static IEnumerator AtlasCapture_web3confirm(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_web3confirm", ok = true };
            report.actions.Add(m);

            string err = null;
            object popupView = null;
            object request = null;

            try
            {
                Type viewType = FindType("DCL.AuthenticationScreenFlow.Web3ConfirmationPopupView");
                if (viewType == null) { err = "web3confirm: Web3ConfirmationPopupView type not found"; }
                else
                {
                    var found = UnityEngine.Object.FindObjectsByType(viewType, FindObjectsInactive.Include);
                    if (found != null && found.Length > 0) popupView = found[0];
                    if (popupView == null)
                        err = "web3confirm: skipped:no-Web3ConfirmationPopupView-instance (auth plugin not initialized?)";
                    else
                    {

                        Type reqType = FindType("DCL.Web3.Authenticators.TransactionConfirmationRequest");
                        if (reqType == null) { err = "web3confirm: TransactionConfirmationRequest type not found"; }
                        else
                        {
                            request = System.Activator.CreateInstance(reqType);
                            reqType.GetProperty("Method")?.SetValue(request, "eth_sendTransaction");
                            reqType.GetProperty("EstimatedGasFeeEth")?.SetValue(request, "0.0012");
                            reqType.GetProperty("BalanceEth")?.SetValue(request, "1.2345");

                        }
                    }
                }
            }
            catch (System.Exception e) { err = "web3confirm: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            try
            {
                System.Reflection.MethodInfo showM = popupView.GetType().GetMethod(
                    "ShowAsync",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new Type[] { request.GetType() },
                    null);
                if (showM == null) { err = "web3confirm: skipped:ShowAsync(TransactionConfirmationRequest)-not-found"; }
                else
                    showM.Invoke(popupView, new object[] { request });
            }
            catch (System.Exception e) { err = "web3confirm: show-failed: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 24; i++) yield return null;
            yield return CaptureShot("web3confirm");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_sceneloading(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_sceneloading", ok = true };
            report.actions.Add(m);

            string err = null;
            object paramInstance = null;
            Type controllerType = null;

            try
            {

                Type reportType = FindType("DCL.Utilities.AsyncLoadProcessReport");
                if (reportType == null) { err = "sceneloading: AsyncLoadProcessReport type not found"; }
                else
                {
                    MethodInfo createMethod = reportType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                    if (createMethod == null) { err = "sceneloading: AsyncLoadProcessReport.Create not found"; }
                    else
                    {
                        object asyncReport = createMethod.Invoke(null, new object[] { System.Threading.CancellationToken.None });
                        if (asyncReport == null) { err = "sceneloading: AsyncLoadProcessReport.Create returned null"; }
                        else
                        {

                            Type paramsType = FindType("DCL.SceneLoadingScreens.SceneLoadingScreenController+Params");
                            if (paramsType == null) { err = "sceneloading: SceneLoadingScreenController+Params type not found"; }
                            else
                            {
                                ConstructorInfo ctor = paramsType.GetConstructor(new[] { reportType });
                                if (ctor == null) { err = "sceneloading: Params(AsyncLoadProcessReport) ctor not found"; }
                                else
                                {
                                    paramInstance = ctor.Invoke(new[] { asyncReport });
                                    if (paramInstance == null) { err = "sceneloading: Params ctor returned null"; }
                                    else
                                    {
                                        controllerType = FindType("DCL.SceneLoadingScreens.SceneLoadingScreenController");
                                        if (controllerType == null) { err = "sceneloading: SceneLoadingScreenController type not found"; }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "sceneloading: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            string showErr = null;
            bool shown = false;
            try
            {
                shown = TryShowPanel(mvcManager, controllerType, paramInstance, out showErr);
            }
            catch (System.Exception e) { showErr = (e.InnerException?.Message ?? e.Message); }
            if (!shown) { m.error = "sceneloading: " + (showErr ?? "TryShowPanel failed"); yield break; }

            for (int i = 0; i < 216; i++) yield return null;

            string verifyErr = null;
            if (!VerifyShown(mvcManager, lastPanelKey, out verifyErr)) { m.error = "sceneloading: " + verifyErr; yield break; }

            yield return CaptureShot("sceneloading");
            m.error = "shown";
        }

private static IEnumerator AtlasCapture_minspecs(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
{
    var m = new PhaseMarker { label = "atlas_minspecs", ok = true };
    report.actions.Add(m);

    string err = null;
    object provideUniTask = null;
    object webBrowser = null, analytics = null;
    try
    {
        if (mvcManager == null) { err = "minspecs: mvcManager is null"; }
        else if (FindControllerByTypeName(mvcManager, "MinimumSpecsScreenController") != null)
        {

            err = null; webBrowser = "ALREADY";
        }
        else
        {
            object loader = FindMainSceneLoader();
            if (loader == null) { err = "minspecs: skipped:no-main-scene-loader"; }
            else
            {
                object bootstrap = GetMember(loader, "bootstrapContainer");
                object dynamicSettings = GetMember(loader, "dynamicSettings");
                if (bootstrap == null) err = "minspecs: bootstrapContainer null (not booted yet)";
                else if (dynamicSettings == null) err = "minspecs: dynamicSettings null";
                else
                {
                    webBrowser = GetMember(bootstrap, "WebBrowser");
                    object analyticsContainer = GetMember(bootstrap, "Analytics");
                    analytics = analyticsContainer != null ? GetMember(analyticsContainer, "Controller") : null;
                    object assetsProvisioner = GetMember(bootstrap, "AssetsProvisioner");
                    object prefabRef = GetMember(dynamicSettings, "MinimumSpecsScreenPrefab");
                    if (webBrowser == null) err = "minspecs: WebBrowser null";
                    else if (analytics == null) err = "minspecs: Analytics.Controller null";
                    else if (assetsProvisioner == null) err = "minspecs: AssetsProvisioner null";
                    else if (prefabRef == null) err = "minspecs: MinimumSpecsScreenPrefab ref null";
                    else
                    {

                        System.Reflection.MethodInfo provide = null;
                        foreach (var mi in assetsProvisioner.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (mi.Name != "ProvideMainAssetAsync" || !mi.IsGenericMethodDefinition) continue;
                            var ps = mi.GetParameters();
                            if (ps.Length != 2) continue;
                            string pn = ps[0].ParameterType.Name;
                            if (pn.StartsWith("AssetReferenceT")) { provide = mi; break; }
                        }
                        if (provide == null) err = "minspecs: ProvideMainAssetAsync<T>(AssetReferenceT) overload not found";
                        else
                        {
                            var bound = provide.MakeGenericMethod(typeof(GameObject));
                            provideUniTask = bound.Invoke(assetsProvisioner, new object[] { prefabRef, System.Threading.CancellationToken.None });
                            if (provideUniTask == null) err = "minspecs: ProvideMainAssetAsync returned null";
                        }
                    }
                }
            }
        }
    }
    catch (System.Exception e) { err = "minspecs: " + (e.InnerException?.Message ?? e.Message); }
    if (err != null) { m.error = "not-shown: " + err; yield break; }

    object providedAsset = null;
    bool alreadyRegistered = ReferenceEquals(webBrowser, "ALREADY");
    if (!alreadyRegistered && provideUniTask != null)
    {
        yield return AwaitUniTask(provideUniTask);
        if (awaitedError != null) { m.error = "not-shown: minspecs: provide-prefab: " + awaitedError; yield break; }
        providedAsset = awaitedResult;
    }

    try
    {
        if (!alreadyRegistered)
        {
            object prefabGo = providedAsset != null ? GetMember(providedAsset, "Value") : null;
            if (prefabGo == null) { err = "minspecs: provided prefab Value null"; }
            else
            {
                Type viewType = FindType("DCL.ApplicationMinimumSpecsGuard.MinimumSpecsScreenView");
                Type ctlType = FindType("DCL.ApplicationMinimumSpecsGuard.MinimumSpecsScreenController");
                Type specResultType = FindType("DCL.ApplicationMinimumSpecsGuard.SpecResult");
                Type specCategoryType = FindType("DCL.ApplicationMinimumSpecsGuard.SpecCategory");
                if (viewType == null || ctlType == null || specResultType == null || specCategoryType == null)
                    err = "minspecs: type lookup failed (view/ctl/SpecResult/SpecCategory)";
                else
                {

                    var getComp = typeof(GameObject).GetMethod("GetComponent", new[] { typeof(Type) });
                    object viewComp = getComp.Invoke(prefabGo, new object[] { viewType });
                    if (viewComp == null) err = "minspecs: prefab has no MinimumSpecsScreenView component";
                    else
                    {

                        var srCtor = specResultType.GetConstructor(new[] { specCategoryType, typeof(bool), typeof(string), typeof(string) });
                        if (srCtor == null) err = "minspecs: SpecResult ctor not found";
                        else
                        {
                            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(specResultType);
                            var list = (System.Collections.IList)Activator.CreateInstance(listType);

                            string[][] rows = {
                                new[]{"CPU", "Intel Core i5-8400 / Ryzen 5 2600", "Intel Core i3-7100"},
                                new[]{"RAM", "16 GB",                             "8 GB"},
                                new[]{"GPU", "NVIDIA GTX 1060 / AMD RX 580",     "Intel UHD Graphics 630"},
                            };
                            foreach (var r in rows)
                            {
                                object cat = Enum.Parse(specCategoryType, r[0]);
                                list.Add(srCtor.Invoke(new object[] { cat, false, r[1], r[2] }));
                            }

                            System.Reflection.MethodInfo createLazily = null;
                            foreach (var mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                                if (mi.Name == "CreateLazily" && mi.IsGenericMethodDefinition) { createLazily = mi; break; }
                            if (createLazily == null) err = "minspecs: CreateLazily not found";
                            else
                            {
                                object viewFactory = createLazily.MakeGenericMethod(viewType)
                                    .Invoke(null, new object[] { viewComp, null });

                                var ctlCtor = ctlType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                                System.Reflection.ConstructorInfo chosen = null;
                                foreach (var c in ctlCtor) if (c.GetParameters().Length == 4) { chosen = c; break; }
                                if (chosen == null) err = "minspecs: MinimumSpecsScreenController(4-arg) ctor not found";
                                else
                                {
                                    object controller = chosen.Invoke(new object[] { viewFactory, webBrowser, analytics, list });

                                    System.Reflection.MethodInfo issue0 = null;
                                    foreach (var mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                                        if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 0) { issue0 = mi; break; }
                                    if (issue0 == null) err = "minspecs: 0-arg IssueCommand not found";
                                    else
                                    {
                                        object command = issue0.Invoke(null, null);
                                        Type[] cmdArgs = command.GetType().GetGenericArguments();

                                        System.Reflection.MethodInfo regGen = null;
                                        foreach (var mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                            if (mi.Name == "RegisterController" && mi.IsGenericMethodDefinition) { regGen = mi; break; }
                                        if (regGen == null) err = "minspecs: RegisterController not found";
                                        else
                                        {
                                            regGen.MakeGenericMethod(cmdArgs).Invoke(mvcManager, new object[] { controller });

                                            System.Reflection.MethodInfo showAsync = null;
                                            foreach (var mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                                if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                                            if (showAsync == null) err = "minspecs: ShowAsync not found";
                                            else
                                                showAsync.MakeGenericMethod(cmdArgs)
                                                    .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {

            Type ctlType = FindType("DCL.ApplicationMinimumSpecsGuard.MinimumSpecsScreenController");
            System.Reflection.MethodInfo issue0 = null;
            if (ctlType != null)
                foreach (var mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 0) { issue0 = mi; break; }
            if (issue0 == null) err = "minspecs: 0-arg IssueCommand not found (already-registered path)";
            else
            {
                object command = issue0.Invoke(null, null);
                Type[] cmdArgs = command.GetType().GetGenericArguments();
                System.Reflection.MethodInfo showAsync = null;
                foreach (var mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                if (showAsync == null) err = "minspecs: ShowAsync not found (already-registered path)";
                else showAsync.MakeGenericMethod(cmdArgs).Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
            }
        }
    }
    catch (System.Exception e) { err = "minspecs: " + (e.InnerException?.Message ?? e.Message); }
    if (err != null) { m.error = "not-shown: " + err; yield break; }

    for (int i = 0; i < 18; i++) yield return null;
    yield return CaptureShot("minspecs");

    string rerr = null;
    try
    {
        object ctl = FindControllerByTypeName(mvcManager, "MinimumSpecsScreenController");
        string st = ctl != null ? (GetPublicProperty(ctl, "State")?.ToString() ?? "?") : null;
        if (ctl == null) rerr = "controller-not-found-after-show";
        else if (st == "ViewHidden" || st == "ViewHiding") rerr = "State=" + st;
    }
    catch (System.Exception e) { rerr = "verify-failed: " + (e.InnerException?.Message ?? e.Message); }
    m.error = rerr != null ? ("not-shown: " + rerr) : "shown";
}

        private static IEnumerator AtlasCapture_updaterequired(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_updaterequired", ok = true };
            report.actions.Add(m);

            string err = null;
            object command = null;
            Type[] cmdArgs = null;
            MethodInfo showAsync = null;
            Type panelKey = null;

            try
            {
                if (mvcManager == null) { err = "updaterequired: mvcManager null"; }
                else
                {
                    Type ctlType  = FindType("DCL.AuthenticationScreenFlow.LauncherRedirectionScreenController");
                    Type viewType = FindType("DCL.AuthenticationScreenFlow.LauncherRedirectionScreenView");
                    if (ctlType == null || viewType == null) { err = "updaterequired: controller/view type not found"; }
                    else
                    {

                        MethodInfo issue0 = null;
                        foreach (var mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                            if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 0) { issue0 = mi; break; }
                        if (issue0 == null) { err = "updaterequired: 0-arg IssueCommand not found"; }
                        else
                        {
                            command = issue0.Invoke(null, null);
                            cmdArgs = command.GetType().GetGenericArguments();

                            object existing = FindControllerByTypeName(mvcManager, "LauncherRedirectionScreenController");
                            if (existing == null)
                            {
                                var prefabGo = UnityEditor.AssetDatabase.LoadAssetAtPath(
                                    "Assets/DCL/ApplicationsGuards/ApplicationVersionGuard/VersionUpdateScreen.prefab",
                                    typeof(UnityEngine.GameObject)) as UnityEngine.GameObject;
                                if (prefabGo == null) { err = "updaterequired: prefab not found via AssetDatabase"; }
                                else
                                {
                                    object viewComp = prefabGo.GetComponent(viewType);
                                    if (viewComp == null) viewComp = prefabGo.GetComponentInChildren(viewType, true);
                                    if (viewComp == null) { err = "updaterequired: prefab missing LauncherRedirectionScreenView"; }
                                    else
                                    {

                                        MethodInfo createLazily = null;
                                        foreach (var mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                                            if (mi.Name == "CreateLazily" && mi.IsGenericMethodDefinition) { createLazily = mi; break; }
                                        if (createLazily == null) { err = "updaterequired: CreateLazily not found"; }
                                        else
                                        {
                                            object viewFactory = createLazily.MakeGenericMethod(viewType).Invoke(null, new object[] { viewComp, null });

                                            ConstructorInfo ctor = null;
                                            foreach (var ci in ctlType.GetConstructors())
                                                if (ci.GetParameters().Length == 4) { ctor = ci; break; }
                                            if (ctor == null) { err = "updaterequired: 4-arg controller ctor not found"; }
                                            else
                                            {

                                                object controller = ctor.Invoke(new object[] { null, viewFactory, "1.0.0-capture", "9.9.9-latest" });

                                                MethodInfo regGen = null;
                                                foreach (var mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                                    if (mi.Name == "RegisterController" && mi.IsGenericMethodDefinition) { regGen = mi; break; }
                                                if (regGen == null) { err = "updaterequired: RegisterController not found"; }
                                                else regGen.MakeGenericMethod(cmdArgs).Invoke(mvcManager, new object[] { controller });
                                            }
                                        }
                                    }
                                }
                            }

                            if (err == null)
                            {
                                foreach (var mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                                if (showAsync == null) err = "updaterequired: ShowAsync not found";
                                else { Type iface = FindType("MVC.IController`2"); panelKey = iface != null ? iface.MakeGenericType(cmdArgs) : null; }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "updaterequired: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            try { showAsync.MakeGenericMethod(cmdArgs).Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None }); }
            catch (System.Exception e) { err = "updaterequired: show: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 24; i++) yield return null;
            yield return CaptureShot("updaterequired");

            string vErr = "no panel key";
            bool shown = panelKey != null && VerifyShown(mvcManager, panelKey, out vErr);
            m.error = shown ? "shown" : ("shown; verify: " + (vErr ?? "?"));
        }

        private static IEnumerator AtlasCapture_connectionerror(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_connectionerror", ok = true };
            report.actions.Add(m);

            string err = null;
            object inputObj = null;
            try
            {
                Type controllerT = FindType("DCL.UI.ErrorPopup.ErrorPopupWithRetryController");
                Type inputT      = FindType("DCL.UI.ErrorPopup.ErrorPopupWithRetryController+Input");
                Type iconTypeT   = FindType("DCL.UI.ErrorPopup.ErrorPopupWithRetryController+IconType");

                if (controllerT == null || inputT == null || iconTypeT == null)
                    err = "connectionerror: types not found (ErrorPopupWithRetryController / Input / IconType)";
                else
                {

                    object iconValue = Enum.Parse(iconTypeT, "CONNECTION_LOST");
                    inputObj = Activator.CreateInstance(inputT, new object[]
                    {
                        "Connection Error",
                        "We were unable to connect to Decentraland. Please verify your connection and retry.",
                        "Continue",
                        "Exit Application",
                        iconValue
                    });
                }
            }
            catch (System.Exception e) { err = "connectionerror: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            if (!TryShowPanelByName(mvcManager, "DCL.UI.ErrorPopup.ErrorPopupWithRetryController", inputObj, out err))
            { m.error = "connectionerror: " + err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("connectionerror");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_duplicateidentity(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_duplicateidentity", ok = true };
            report.actions.Add(m);

            string err = null;
            object command = null;
            Type[] cmdArgs = null;
            MethodInfo showAsync = null;
            Type panelKey = null;

            try
            {
                if (mvcManager == null) { err = "duplicateidentity: mvcManager null"; }
                else
                {
                    Type ctlType  = FindType("DCL.UI.DuplicateIdentityPopup.DuplicateIdentityWindowController");
                    Type viewType = FindType("DCL.UI.DuplicateIdentityPopup.DuplicateIdentityWindowView");
                    if (ctlType == null || viewType == null) { err = "duplicateidentity: controller/view type not found"; }
                    else
                    {

                        MethodInfo issue0 = null;
                        foreach (var mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                            if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 0) { issue0 = mi; break; }
                        if (issue0 == null) { err = "duplicateidentity: 0-arg IssueCommand not found"; }
                        else
                        {
                            command = issue0.Invoke(null, null);
                            cmdArgs = command.GetType().GetGenericArguments();

                            object existing = FindControllerByTypeName(mvcManager, "DuplicateIdentityWindowController");
                            if (existing == null)
                            {
                                var prefabGo = UnityEditor.AssetDatabase.LoadAssetAtPath(
                                    "Assets/DCL/UI/DuplicateIdentityPopup/DuplicateIdentityWindow.prefab",
                                    typeof(UnityEngine.GameObject)) as UnityEngine.GameObject;
                                if (prefabGo == null) { err = "duplicateidentity: prefab not found via AssetDatabase"; }
                                else
                                {
                                    object viewComp = prefabGo.GetComponent(viewType);
                                    if (viewComp == null) viewComp = prefabGo.GetComponentInChildren(viewType, true);
                                    if (viewComp == null) { err = "duplicateidentity: prefab missing DuplicateIdentityWindowView"; }
                                    else
                                    {

                                        MethodInfo createLazily = null;
                                        foreach (var mi in ctlType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                                            if (mi.Name == "CreateLazily" && mi.IsGenericMethodDefinition) { createLazily = mi; break; }
                                        if (createLazily == null) { err = "duplicateidentity: CreateLazily not found"; }
                                        else
                                        {
                                            object viewFactory = createLazily.MakeGenericMethod(viewType).Invoke(null, new object[] { viewComp, null });

                                            object controller = ctlType.GetConstructors()[0].Invoke(new object[] { viewFactory });

                                            MethodInfo regGen = null;
                                            foreach (var mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                                if (mi.Name == "RegisterController" && mi.IsGenericMethodDefinition) { regGen = mi; break; }
                                            if (regGen == null) { err = "duplicateidentity: RegisterController not found"; }
                                            else regGen.MakeGenericMethod(cmdArgs).Invoke(mvcManager, new object[] { controller });
                                        }
                                    }
                                }
                            }

                            if (err == null)
                            {
                                foreach (var mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                                if (showAsync == null) err = "duplicateidentity: ShowAsync not found";
                                else { Type iface = FindType("MVC.IController`2"); panelKey = iface != null ? iface.MakeGenericType(cmdArgs) : null; }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "duplicateidentity: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            try { showAsync.MakeGenericMethod(cmdArgs).Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None }); }
            catch (System.Exception e) { err = "duplicateidentity: show: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 24; i++) yield return null;
            yield return CaptureShot("duplicateidentity");

            string vErr = "no panel key";
            bool shown = panelKey != null && VerifyShown(mvcManager, panelKey, out vErr);
            m.error = shown ? "shown" : ("shown; verify: " + (vErr ?? "?"));
        }

        private static IEnumerator AtlasCapture_communities(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_communities", ok = true };
            report.actions.Add(m);

            string err = null;
            bool opened = false;
            try
            {
                if (mvcManager == null) err = "communities: mvcManager null";
                else
                {
                    opened = TryOpenExplorePanel(mvcManager, "Communities", null, out string openErr);
                    if (!opened) err = "communities: TryOpenExplorePanel failed: " + openErr;
                }
            }
            catch (System.Exception e) { err = "communities: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 24; i++) yield return null;

            System.Type filteredViewT = null;
            try { filteredViewT = FindType("DCL.Communities.CommunitiesBrowser.FilteredCommunitiesView"); }
            catch { filteredViewT = null; }

            int resultsCount = 0;
            for (int i = 0; i < 360; i++)
            {
                int polled = 0;
                try
                {
                    if (filteredViewT != null)
                    {
                        UnityEngine.Object[] views = UnityEngine.Resources.FindObjectsOfTypeAll(filteredViewT);
                        if (views != null)
                        {
                            for (int v = 0; v < views.Length; v++)
                            {
                                object cntObj = GetPublicProperty(views[v], "CurrentResultsCount");
                                if (cntObj is int c && c > polled) polled = c;
                            }
                        }
                    }
                }
                catch {  }

                resultsCount = polled;
                if (resultsCount > 0) break;
                yield return null;
            }

            int settle = resultsCount > 0 ? 60 : 240;
            for (int i = 0; i < settle; i++) yield return null;
            yield return CaptureShot("communities");

            string verifyErr = "no panel key";
            bool shown = lastPanelKey != null && VerifyShown(mvcManager, lastPanelKey, out verifyErr);
            m.error = shown ? "shown" : ("not-shown: " + (verifyErr ?? "no panel key"));
        }

        private static IEnumerator AtlasCapture_passport(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_passport", ok = true };
            report.actions.Add(m);

            string err = null;
            object profileTask = null;
            try
            {
                object selfProfile = ReachSelfProfile(dynamicContainer);
                if (selfProfile == null) err = "passport: selfProfile not found on dynamicContainer";
                else
                {
                    MethodInfo profileAsync = null;
                    foreach (MethodInfo mi in selfProfile.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        if (mi.Name == "ProfileAsync") { profileAsync = mi; break; }
                    if (profileAsync == null) err = "passport: ProfileAsync not found";
                    else
                    {

                        object[] args = new object[profileAsync.GetParameters().Length];
                        for (int i = 0; i < args.Length; i++) args[i] = System.Threading.CancellationToken.None;
                        profileTask = profileAsync.Invoke(selfProfile, args);
                    }
                }
            }
            catch (System.Exception e) { err = "passport: " + (e.InnerException?.Message ?? e.Message); }

            if (err != null) { m.error = err; for (int i = 0; i < 18; i++) yield return null; yield return CaptureShot("passport"); yield break; }

            yield return AwaitUniTask(profileTask);
            object profileResult = awaitedResult;
            if (awaitedError != null) { m.error = "passport: profile-fetch: " + awaitedError; for (int i = 0; i < 18; i++) yield return null; yield return CaptureShot("passport"); yield break; }

            err = null;
            Type panelKey = null;
            try
            {
                string userId = profileResult != null ? GetMember(profileResult, "UserId")?.ToString() : null;
                if (string.IsNullOrEmpty(userId)) { err = "passport: own UserId unavailable"; }
                else
                {
                    Type passportControllerT = FindType("DCL.Passport.PassportController");
                    Type passportParamsT = FindType("DCL.Passport.PassportParams");
                    if (passportControllerT == null || passportParamsT == null) err = "passport: passport types not found";
                    else
                    {

                        object passportParams = System.Activator.CreateInstance(passportParamsT, new object[] { userId, null, true });

                        MethodInfo issueCommand = null;
                        foreach (MethodInfo mi in passportControllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                            if (mi.Name == "IssueCommand") { issueCommand = mi; break; }
                        if (issueCommand == null) err = "passport: IssueCommand not found";
                        else
                        {
                            object command = issueCommand.Invoke(null, new object[] { passportParams });
                            if (command == null) err = "passport: IssueCommand returned null";
                            else
                            {
                                MethodInfo showAsync = null;
                                foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                                if (showAsync == null) err = "passport: ShowAsync not found";
                                else
                                {
                                    Type[] genArgs = command.GetType().GetGenericArguments();
                                    showAsync.MakeGenericMethod(genArgs)
                                             .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                                    Type ifaceOpen = FindType("MVC.IController`2");
                                    panelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "passport: " + (e.InnerException?.Message ?? e.Message); }

            if (err != null) { m.error = err; for (int i = 0; i < 18; i++) yield return null; yield return CaptureShot("passport"); yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            string verifyErr = null;
            bool shown = panelKey != null && VerifyShown(mvcManager, panelKey, out verifyErr);
            yield return CaptureShot("passport");
            m.error = shown ? "shown" : ("not-shown: " + (verifyErr ?? "unknown"));
        }

        private static IEnumerator AtlasCapture_communitycreate(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_communitycreate", ok = true };
            report.actions.Add(m);

            string err = null;
            object command = null;
            MethodInfo showAsync = null;
            Type[] genArgs = null;
            Type panelKey = null;
            try
            {
                if (mvcManager == null) { err = "communitycreate: mvcManager null"; }
                else
                {
                    Type controllerT = FindType("DCL.Communities.CommunityCreation.CommunityCreationEditionController");
                    Type paramT = FindType("DCL.Communities.CommunityCreation.CommunityCreationEditionParameter");

                    if (controllerT == null) err = "communitycreate: CommunityCreationEditionController type not found";
                    else if (paramT == null) err = "communitycreate: CommunityCreationEditionParameter type not found";
                    else
                    {

                        object param = paramT.GetConstructors()[0].Invoke(new object[] { true, string.Empty, null });

                        MethodInfo issue = null;
                        foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                            if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 1) { issue = mi; break; }
                        if (issue == null) err = "communitycreate: IssueCommand(1-arg) not found";
                        else
                        {
                            command = issue.Invoke(null, new[] { param });
                            if (command == null) err = "communitycreate: IssueCommand returned null";
                            else
                            {

                                foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                                if (showAsync == null) err = "communitycreate: ShowAsync not found";
                                else
                                {
                                    genArgs = command.GetType().GetGenericArguments();
                                    Type ifaceOpen = FindType("MVC.IController`2");
                                    panelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "communitycreate: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            try
            {
                showAsync.MakeGenericMethod(genArgs)
                         .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
            }
            catch (System.Exception e) { err = "communitycreate: ShowAsync failed: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 90; i++) yield return null;

            string note = "shown";
            if (panelKey != null && !VerifyShown(mvcManager, panelKey, out string verifyErr))
                note = "not-shown: " + (verifyErr ?? "?");

            yield return CaptureShot("communitycreate");
            m.error = note;
        }

        private static IEnumerator AtlasCapture_createcommunity(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_createcommunity", ok = true };
            report.actions.Add(m);

            string err = null;
            object command = null;
            MethodInfo showAsync = null;
            Type[] genArgs = null;
            Type panelKey = null;
            try
            {
                if (mvcManager == null) { err = "createcommunity: mvcManager null"; }
                else
                {
                    Type controllerT = FindType("DCL.Communities.CommunityCreation.CommunityCreationEditionController");
                    Type paramT = FindType("DCL.Communities.CommunityCreation.CommunityCreationEditionParameter");
                    if (controllerT == null) err = "createcommunity: CommunityCreationEditionController type not found";
                    else if (paramT == null) err = "createcommunity: CommunityCreationEditionParameter type not found";
                    else
                    {

                        object param = paramT.GetConstructors()[0].Invoke(new object[] { true, string.Empty, null });

                        MethodInfo issue = null;
                        foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                            if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 1) { issue = mi; break; }
                        if (issue == null) err = "createcommunity: IssueCommand(1-arg) not found";
                        else
                        {
                            command = issue.Invoke(null, new[] { param });
                            if (command == null) err = "createcommunity: IssueCommand returned null";
                            else
                            {
                                foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                                if (showAsync == null) err = "createcommunity: ShowAsync not found";
                                else
                                {
                                    genArgs = command.GetType().GetGenericArguments();
                                    Type ifaceOpen = FindType("MVC.IController`2");
                                    panelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "createcommunity: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            try
            {
                showAsync.MakeGenericMethod(genArgs)
                         .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
            }
            catch (System.Exception e) { err = "createcommunity: ShowAsync failed: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 90; i++) yield return null;

            object createTask = null;
            try
            {
                object creationController = FindControllerByTypeName(mvcManager, "CommunityCreationEditionController");
                if (creationController == null) err = "createcommunity: CommunityCreationEditionController not registered";
                else
                {
                    object dataProvider = GetPrivateField(creationController, "dataProvider");
                    if (dataProvider == null) err = "createcommunity: dataProvider field not found";
                    else
                    {

                        Type privacyT = FindType("DCL.Communities.CommunitiesDataProvider.DTOs.CommunityPrivacy");
                        Type visibilityT = FindType("DCL.Communities.CommunitiesDataProvider.DTOs.CommunityVisibility");
                        if (privacyT == null) err = "createcommunity: CommunityPrivacy enum not found";
                        else if (visibilityT == null) err = "createcommunity: CommunityVisibility enum not found";
                        else
                        {
                            object privacyPublic = System.Enum.Parse(privacyT, "public");
                            object visibilityAll = System.Enum.Parse(visibilityT, "all");

                            MethodInfo createMi = dataProvider.GetType()
                                .GetMethod("CreateOrUpdateCommunityAsync", BindingFlags.Public | BindingFlags.Instance);
                            if (createMi == null) err = "createcommunity: CreateOrUpdateCommunityAsync not found";
                            else
                            {
                                var lands = new System.Collections.Generic.List<string>();
                                var worlds = new System.Collections.Generic.List<string>();

                                object[] args = new object[]
                                {
                                    null,
                                    "Evaristo Test Community",
                                    "Created via Atlas in-editor driver (authorized).",
                                    null,
                                    lands,
                                    worlds,
                                    privacyPublic,
                                    visibilityAll,
                                    System.Threading.CancellationToken.None
                                };
                                createTask = createMi.Invoke(dataProvider, args);
                                if (createTask == null) err = "createcommunity: create UniTask null";
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "createcommunity: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            yield return AwaitUniTask(createTask);

            string createdId = null, createdName = null;
            string readErr = null;
            try
            {
                if (awaitedError == null && awaitedResult != null)
                {
                    object data = GetMember(awaitedResult, "data");
                    if (data != null)
                    {
                        createdId = GetMember(data, "id")?.ToString();
                        createdName = GetMember(data, "name")?.ToString();
                    }
                }
            }
            catch (System.Exception e) { readErr = e.InnerException?.Message ?? e.Message; }

            for (int i = 0; i < 120; i++) yield return null;
            yield return CaptureShot("communitycreate");

            if (awaitedError != null)
                m.error = "create-failed: " + awaitedError;
            else if (!string.IsNullOrEmpty(createdId))
                m.error = "shown; created id=" + createdId + " name=" + (createdName ?? "?");
            else if (readErr != null)
                m.error = "shown; created (id-read-error: " + readErr + ")";
            else
                m.error = "shown; created (id not in response)";
        }

        private static IEnumerator AtlasCapture_badgesdetail(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_badgesdetail", ok = true };
            report.actions.Add(m);

            string err = null;
            object selfProfile = null;
            object profileTask = null;

            try
            {
                selfProfile = ReachSelfProfile(dynamicContainer);
                if (selfProfile == null) err = "badgesdetail: selfProfile not found";
                else
                {
                    profileTask = TryInvoke(selfProfile, "ProfileAsync",
                        new object[] { System.Threading.CancellationToken.None }, out string ierr);
                    if (profileTask == null) err = "badgesdetail: ProfileAsync invoke failed: " + ierr;
                }
            }
            catch (System.Exception e) { err = "badgesdetail: " + (e.InnerException?.Message ?? e.Message); }

            if (err != null) { m.error = err; for (int i = 0; i < 18; i++) yield return null; yield return CaptureShot("badgesdetail"); yield break; }

            yield return AwaitUniTask(profileTask);
            if (awaitedError != null) { m.error = "badgesdetail: ProfileAsync failed: " + awaitedError; yield break; }

            object profile = awaitedResult;
            string userId = null;

            object passportParam = null;
            try
            {
                if (profile == null) err = "badgesdetail: profile result null";
                else
                {
                    userId = GetPublicProperty(profile, "UserId") as string;
                    if (string.IsNullOrEmpty(userId)) err = "badgesdetail: userId empty";
                }

                if (err == null)
                {

                    Type paramsT = FindType("DCL.Passport.PassportParams");
                    if (paramsT == null) err = "badgesdetail: PassportParams type not found";
                    else
                    {
                        ConstructorInfo ctor = paramsT.GetConstructor(new[] { typeof(string), typeof(string), typeof(bool) });
                        if (ctor == null) err = "badgesdetail: PassportParams constructor not found";
                        else
                            passportParam = ctor.Invoke(new object[] { userId, null, true });
                    }
                }

                if (err == null)
                {

                    if (!TryShowPanelByName(mvcManager, "DCL.Passport.PassportController", passportParam, out string showErr))
                        err = "badgesdetail: open passport failed: " + showErr;
                }
            }
            catch (System.Exception e) { err = "badgesdetail: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            try
            {
                object passportCtl = FindControllerByTypeName(mvcManager, "PassportController");
                if (passportCtl != null)
                {
                    MethodInfo openBadges = passportCtl.GetType().GetMethod("OpenBadgesSection",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (openBadges != null)
                    {

                        ParameterInfo[] ps = openBadges.GetParameters();
                        object[] callArgs = ps.Length == 1 ? new object[] { null } : new object[0];
                        openBadges.Invoke(passportCtl, callArgs);
                    }
                }

            }
            catch (System.Exception) {  }

            object badgeMainContainer = null;
            for (int i = 0; i < 480; i++)
            {
                bool detailShown = false;
                try
                {
                    if (badgeMainContainer == null)
                    {
                        object passportCtl2 = FindControllerByTypeName(mvcManager, "PassportController");
                        object passView = passportCtl2 != null ? GetMember(passportCtl2, "viewInstance") : null;
                        object badgeInfoView = passView != null ? GetMember(passView, "BadgeInfoModuleView") : null;
                        badgeMainContainer = badgeInfoView != null ? GetMember(badgeInfoView, "MainContainer") : null;
                    }
                    object active = badgeMainContainer != null ? GetMember(badgeMainContainer, "activeSelf") : null;
                    if (active is bool ab) detailShown = ab;
                }
                catch { detailShown = false; }
                if (detailShown) break;
                yield return null;
            }

            for (int i = 0; i < 48; i++) yield return null;

            yield return CaptureShot("badgesdetail");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_communitycard(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_communitycard", ok = true };
            report.actions.Add(m);
            yield return HideExplorePanel(mvcManager);

            string err = null;
            object communitiesDataProvider = null;
            object listTask = null;
            try
            {
                if (mvcManager == null) { err = "communitycard: mvcManager null"; }
                else
                {
                    object cardController = FindControllerByTypeName(mvcManager, "CommunityCardController");
                    if (cardController == null) err = "communitycard: CommunityCardController not registered";
                    else
                    {
                        communitiesDataProvider = GetPrivateField(cardController, "communitiesDataProvider");
                        if (communitiesDataProvider == null) err = "communitycard: communitiesDataProvider field not found";
                        else
                        {

                            MethodInfo getCommunities = communitiesDataProvider.GetType()
                                .GetMethod("GetUserCommunitiesAsync", BindingFlags.Public | BindingFlags.Instance);
                            if (getCommunities == null) err = "communitycard: GetUserCommunitiesAsync not found";
                            else
                                listTask = getCommunities.Invoke(communitiesDataProvider, new object[]
                                {
                                    "", false, 1, 10, System.Threading.CancellationToken.None, false, false
                                });
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "communitycard: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }
            if (listTask == null) { m.error = "communitycard: list task null"; yield break; }

            yield return AwaitUniTask(listTask);
            if (awaitedError != null) { m.error = "communitycard: list fetch: " + awaitedError; yield break; }

            string communityId = null;
            try
            {
                object data = awaitedResult != null ? GetMember(awaitedResult, "data") : null;
                object results = data != null ? GetMember(data, "results") : null;
                if (results is System.Collections.IEnumerable enumerable)
                {
                    foreach (object community in enumerable)
                    {
                        object id = GetMember(community, "id");
                        string ids = id?.ToString();
                        if (!string.IsNullOrEmpty(ids)) { communityId = ids; break; }
                    }
                }
            }
            catch (System.Exception e) { err = "communitycard: id extract: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }
            if (string.IsNullOrEmpty(communityId))
            {

                for (int i = 0; i < 18; i++) yield return null;
                yield return CaptureShot("communitycard");
                m.error = "communitycard: skipped:no-community-id (account in no communities)";
                yield break;
            }

            try
            {
                Type controllerType = FindType("DCL.Communities.CommunitiesCard.CommunityCardController");
                Type paramType = FindType("DCL.Communities.CommunitiesCard.CommunityCardParameter");
                if (controllerType == null || paramType == null) err = "communitycard: card types not found";
                else
                {

                    object param = Activator.CreateInstance(paramType, communityId, null);

                    MethodInfo issueCommand = controllerType.GetMethod("IssueCommand",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                        null, new[] { paramType }, null);
                    if (issueCommand == null) err = "communitycard: IssueCommand(param) not found";
                    else
                    {
                        object command = issueCommand.Invoke(null, new[] { param });
                        if (command == null) err = "communitycard: IssueCommand returned null";
                        else
                        {
                            MethodInfo showAsync = null;
                            foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                            if (showAsync == null) err = "communitycard: ShowAsync not found";
                            else
                            {
                                Type[] genArgs = command.GetType().GetGenericArguments();
                                showAsync.MakeGenericMethod(genArgs)
                                    .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                                Type ifaceOpen = FindType("MVC.IController`2");
                                lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "communitycard: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            bool loaded = false;
            for (int i = 0; i < 360 && !loaded; i++)
            {
                float mainAlpha = 1f;
                float annAlpha = 1f;
                bool readMain = false;
                bool readAnn = false;
                try
                {
                    object cardController = FindControllerByTypeName(mvcManager, "CommunityCardController");
                    object viewInstance = cardController != null ? GetMember(cardController, "viewInstance") : null;
                    if (viewInstance != null)
                    {

                        object mainSkel = GetPrivateField(viewInstance, "loadingObject");
                        if (mainSkel != null)
                        {
                            object cg = GetPrivateField(mainSkel, "loadingCanvasGroup");
                            if (cg is UnityEngine.CanvasGroup mcg) { mainAlpha = mcg.alpha; readMain = true; }
                        }

                        object annView = GetPublicProperty(viewInstance, "AnnouncementsSectionView")
                                         ?? GetPublicField(viewInstance, "AnnouncementsSectionView");
                        if (annView != null)
                        {
                            object annSkel = GetPrivateField(annView, "loadingObject");
                            if (annSkel != null)
                            {
                                object acg = GetPrivateField(annSkel, "loadingCanvasGroup");
                                if (acg is UnityEngine.CanvasGroup accg) { annAlpha = accg.alpha; readAnn = true; }
                            }
                        }
                    }
                }
                catch {  }

                if (readMain && mainAlpha <= 0.05f && (!readAnn || annAlpha <= 0.05f))
                    loaded = true;
                else
                    yield return null;
            }

            for (int i = 0; i < 60; i++) yield return null;

            yield return CaptureShot("communitycard");

            string verifyErr = "no panel key";
            bool shown = lastPanelKey != null && VerifyShown(mvcManager, lastPanelKey, out verifyErr);
            m.error = shown ? "shown" : ("not-shown: " + (verifyErr ?? "no panel key"));
        }

        private static IEnumerator AtlasCapture_friendactions(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_friendactions", ok = true };
            report.actions.Add(m);

            var ctNone = System.Threading.CancellationToken.None;

            string err = null;
            object friendsService = null;
            try
            {
                Type panelControllerT = FindType("DCL.Friends.UI.FriendPanel.FriendsPanelController");
                Type panelParamT = FindType("DCL.Friends.UI.FriendPanel.FriendsPanelParameter");
                Type tabT = FindType("DCL.Friends.UI.FriendPanel.FriendsPanelController+FriendsPanelTab");
                if (panelControllerT == null || panelParamT == null || tabT == null)
                {
                    err = "friendactions: Friends panel types not found";
                }
                else
                {
                    object friendsTab = System.Enum.Parse(tabT, "FRIENDS");
                    object panelParam = System.Activator.CreateInstance(panelParamT, new object[] { friendsTab });
                    if (!TryShowPanelByName(mvcManager, "DCL.Friends.UI.FriendPanel.FriendsPanelController", panelParam, out err))
                    {

                    }
                }
            }
            catch (System.Exception e) { err = "friendactions: open-friends failed: " + (e.InnerException?.Message ?? e.Message); }

            for (int i = 0; i < 18; i++) yield return null;

            try
            {
                object panelController = FindControllerByTypeName(mvcManager, "FriendsPanelController");
                if (panelController != null)
                {
                    object section = GetPrivateField(panelController, "friendSectionController")
                                  ?? GetPrivateField(panelController, "friendSectionControllerConnectivity");
                    if (section != null)
                    {
                        object requestManager = GetPrivateField(section, "requestManager");
                        if (requestManager != null)
                            friendsService = GetPrivateField(requestManager, "friendsService");
                    }
                }
            }
            catch (System.Exception e) { if (err == null) err = "friendactions: service-lookup failed: " + (e.InnerException?.Message ?? e.Message); }

            if (friendsService == null)
            {
                m.error = "friendactions: IFriendsService not reachable" + (err != null ? " (" + err + ")" : "") + "; captured friends panel only";
                for (int i = 0; i < 6; i++) yield return null;
                yield return CaptureShot("friendactions");
                yield break;
            }

            object friendsTask = null;
            try
            {
                System.Reflection.MethodInfo getFriends = friendsService.GetType().GetMethod(
                    "GetFriendsAsync",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new[] { typeof(int), typeof(int), typeof(System.Threading.CancellationToken) },
                    null);
                if (getFriends == null) err = "friendactions: GetFriendsAsync not found";
                else friendsTask = getFriends.Invoke(friendsService, new object[] { 0, 1, ctNone });
            }
            catch (System.Exception e) { err = "friendactions: GetFriendsAsync invoke failed: " + (e.InnerException?.Message ?? e.Message); }

            if (friendsTask == null)
            {
                m.error = err ?? "friendactions: GetFriendsAsync returned null";
                yield return CaptureShot("friendactions");
                yield break;
            }

            yield return AwaitUniTask(friendsTask);
            if (awaitedError != null)
            {
                m.error = "friendactions: GetFriendsAsync failed: " + awaitedError + " (dataGated)";
                yield return CaptureShot("friendactions");
                yield break;
            }

            string friendUserId = null;
            try
            {
                if (awaitedResult != null)
                {
                    object friendsList = GetPublicProperty(awaitedResult, "Friends");
                    if (friendsList is System.Collections.IEnumerable en)
                    {
                        var it = en.GetEnumerator();
                        if (it.MoveNext() && it.Current != null)
                        {
                            object uid = GetMember(it.Current, "UserId");
                            friendUserId = uid != null ? uid.ToString() : null;
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "friendactions: friend-extract failed: " + (e.InnerException?.Message ?? e.Message); }

            if (string.IsNullOrEmpty(friendUserId))
            {
                m.error = "friendactions: no friends available (dataGated:no-friends); captured friends panel only";
                yield return CaptureShot("friendactions");
                yield break;
            }

            try
            {
                Type passportParamsT = FindType("DCL.Passport.PassportParams");
                if (passportParamsT == null) err = "friendactions: PassportParams not found";
                else
                {
                    object passportParams = System.Activator.CreateInstance(passportParamsT, new object[] { friendUserId, null, false });
                    if (!TryShowPanelByName(mvcManager, "DCL.Passport.PassportController", passportParams, out err))
                    {

                    }
                }
            }
            catch (System.Exception e) { err = "friendactions: passport-open failed: " + (e.InnerException?.Message ?? e.Message); }

            for (int i = 0; i < 18; i++) yield return null;

            bool clicked = false;
            try
            {
                object passportController = FindControllerByTypeName(mvcManager, "PassportController");
                if (passportController != null)
                {

                    object viewInstance = GetMember(passportController, "viewInstance");
                    if (viewInstance != null)
                    {
                        object removeFriendButton = GetPublicProperty(viewInstance, "RemoveFriendButton");
                        if (removeFriendButton != null)
                        {
                            object onClick = GetPublicProperty(removeFriendButton, "onClick");
                            if (onClick != null)
                            {
                                System.Reflection.MethodInfo invoke = onClick.GetType().GetMethod(
                                    "Invoke", System.Type.EmptyTypes);
                                if (invoke != null) { invoke.Invoke(onClick, null); clicked = true; }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "friendactions: remove-friend-click failed: " + (e.InnerException?.Message ?? e.Message); }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("friendactions");
            m.error = clicked ? "shown" : ("friendactions: passport shown, unfriend-confirm not triggered" + (err != null ? " (" + err + ")" : ""));
        }

        private static IEnumerator AtlasCapture_communitymembers(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_communitymembers", ok = true };
            report.actions.Add(m);
            yield return HideExplorePanel(mvcManager);

            string err = null;
            object dataProvider = null;
            object listTask = null;

            try
            {
                if (mvcManager == null) { err = "communitymembers: mvcManager null"; }
                else
                {
                    object dict = GetPublicProperty(mvcManager, "Controllers");
                    if (dict == null)
                    {
                        object core = GetPrivateField(mvcManager, "core");
                        if (core != null) dict = GetPublicProperty(core, "Controllers");
                    }
                    object values = dict != null ? GetPublicProperty(dict, "Values") : null;
                    if (values is System.Collections.IEnumerable en)
                    {
                        foreach (object ctrl in en)
                        {
                            if (ctrl == null) continue;
                            foreach (FieldInfo fi in ctrl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                            {
                                object fv = fi.GetValue(ctrl);
                                if (fv != null && fv.GetType().Name == "CommunitiesDataProvider") { dataProvider = fv; break; }
                            }
                            if (dataProvider != null) break;
                        }
                    }

                    if (dataProvider == null)
                        err = "communitymembers: skipped:no-CommunitiesDataProvider-reachable (no registered controller holds it yet)";
                    else
                    {

                        var ct = System.Threading.CancellationToken.None;
                        MethodInfo mi = null;
                        foreach (MethodInfo c in dataProvider.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (c.Name == "GetUserCommunitiesAsync") { mi = c; break; }
                        if (mi == null) err = "communitymembers: GetUserCommunitiesAsync not found";
                        else
                        {
                            ParameterInfo[] ps = mi.GetParameters();
                            object[] args = new object[ps.Length];
                            for (int i = 0; i < ps.Length; i++)
                            {
                                if (ps[i].ParameterType == typeof(string)) args[i] = "";
                                else if (ps[i].ParameterType == typeof(bool)) args[i] = false;
                                else if (ps[i].ParameterType == typeof(int)) args[i] = (ps[i].Name == "pageNumber") ? 1 : 20;
                                else if (ps[i].ParameterType == typeof(System.Threading.CancellationToken)) args[i] = ct;
                                else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                            }
                            listTask = mi.Invoke(dataProvider, args);
                            if (listTask == null) err = "communitymembers: GetUserCommunitiesAsync returned null";
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "communitymembers: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            yield return AwaitUniTask(listTask);
            if (awaitedError != null) { m.error = "communitymembers: list-failed: " + awaitedError; yield break; }

            string communityId = null;
            object command = null;
            Type[] genArgs = null;
            try
            {
                object resp = awaitedResult;

                object payload = resp;
                object maybeValue = resp != null ? GetMember(resp, "Value") : null;
                if (maybeValue != null && maybeValue.GetType().Name.Contains("Response")) payload = maybeValue;

                object data = payload != null ? GetMember(payload, "data") : null;
                object results = data != null ? GetMember(data, "results") : null;
                if (results is System.Collections.IEnumerable ren)
                {
                    foreach (object comm in ren)
                    {
                        communityId = GetMember(comm, "id") as string;
                        if (!string.IsNullOrEmpty(communityId)) break;
                    }
                }

                if (string.IsNullOrEmpty(communityId))
                    err = "communitymembers: dataGated:no-community-in-results";
                else
                {
                    Type paramType = FindType("DCL.Communities.CommunitiesCard.CommunityCardParameter");
                    Type controllerType = FindType("DCL.Communities.CommunitiesCard.CommunityCardController");
                    if (paramType == null || controllerType == null) err = "communitymembers: card types not found";
                    else
                    {

                        object param = System.Activator.CreateInstance(paramType, new object[] { communityId, null });

                        MethodInfo issue = null;
                        foreach (MethodInfo c in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                            if (c.Name == "IssueCommand" && c.GetParameters().Length == 1) { issue = c; break; }
                        if (issue == null) err = "communitymembers: IssueCommand(param) not found";
                        else
                        {
                            command = issue.Invoke(null, new[] { param });
                            if (command == null) err = "communitymembers: IssueCommand returned null";
                            else
                            {
                                genArgs = command.GetType().GetGenericArguments();
                                MethodInfo showAsync = null;
                                foreach (MethodInfo c in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    if (c.Name == "ShowAsync" && c.IsGenericMethodDefinition) { showAsync = c; break; }
                                if (showAsync == null) err = "communitymembers: ShowAsync not found";
                                else
                                    showAsync.MakeGenericMethod(genArgs)
                                             .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "communitymembers: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            object cardController = null;
            for (int i = 0; i < 480; i++)
            {
                bool loaded = false;
                try
                {
                    if (cardController == null) cardController = FindControllerByTypeName(mvcManager, "CommunityCardController");
                    object cd = cardController != null ? GetPrivateField(cardController, "communityData") : null;

                    object cdId = cd != null ? GetMember(cd, "id") : null;
                    loaded = cardController != null && !string.IsNullOrEmpty(cdId as string);
                }
                catch { loaded = false; }
                if (loaded) break;
                yield return null;
            }

            Type ifaceOpen = FindType("MVC.IController`2");
            lastPanelKey = (ifaceOpen != null && genArgs != null) ? ifaceOpen.MakeGenericType(genArgs) : null;
            if (lastPanelKey != null && !VerifyShown(mvcManager, lastPanelKey, out string verr))
            { m.error = "communitymembers: not-shown (" + verr + ")"; yield return CaptureShot("communitymembers"); yield break; }

            object viewInst = null;
            for (int i = 0; i < 300; i++)
            {
                bool toggled = false;
                try
                {
                    if (cardController == null) cardController = FindControllerByTypeName(mvcManager, "CommunityCardController");
                    if (viewInst == null) viewInst = cardController != null ? GetMember(cardController, "viewInstance") : null;
                    object cs = viewInst != null ? GetPrivateField(viewInst, "currentSection") : null;
                    toggled = cs != null;
                }
                catch { toggled = false; }
                if (toggled) break;
                yield return null;
            }

            for (int i = 0; i < 30; i++) yield return null;

            object membersCtrl = null;
            object sectionsEnumMembers = null;
            MethodInfo toggleMethod = null;
            try
            {
                if (cardController == null) cardController = FindControllerByTypeName(mvcManager, "CommunityCardController");
                membersCtrl = cardController != null ? GetPrivateField(cardController, "membersListController") : null;

                if (viewInst == null) viewInst = cardController != null ? GetMember(cardController, "viewInstance") : null;
                if (viewInst != null)
                {
                    Type sectionsEnum = FindType("DCL.Communities.CommunitiesCard.CommunityCardView+Sections");
                    sectionsEnumMembers = sectionsEnum != null ? System.Enum.Parse(sectionsEnum, "MEMBERS") : null;
                    if (sectionsEnumMembers != null)
                    {
                        foreach (MethodInfo c in viewInst.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                            if (c.Name == "ToggleSection") { toggleMethod = c; break; }
                        if (toggleMethod != null)
                        {

                            ParameterInfo[] tp = toggleMethod.GetParameters();
                            object[] targs = new object[tp.Length];
                            targs[0] = sectionsEnumMembers;
                            for (int i = 1; i < tp.Length; i++)
                                targs[i] = (tp[i].ParameterType == typeof(bool)) ? (object)true
                                          : (tp[i].HasDefaultValue ? tp[i].DefaultValue : null);
                            toggleMethod.Invoke(viewInst, targs);
                        }
                        else
                        {

                            FieldInfo evtField = viewInst.GetType().GetField("SectionChanged",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            object evtDelegate = evtField != null ? evtField.GetValue(viewInst) : null;
                            if (evtDelegate is System.Delegate del) del.DynamicInvoke(sectionsEnumMembers);
                        }
                    }
                }
            }
            catch {  }

            if (membersCtrl == null)
            {
                if (cardController == null) cardController = FindControllerByTypeName(mvcManager, "CommunityCardController");
                try { membersCtrl = cardController != null ? GetPrivateField(cardController, "membersListController") : null; }
                catch { membersCtrl = null; }
            }

            bool sawFetching = false;
            int settled = 0;
            for (int i = 0; i < 480; i++)
            {
                bool fetching = false;
                bool readable = false;
                try
                {
                    object f = membersCtrl != null ? GetPrivateField(membersCtrl, "isFetching") : null;
                    if (f is bool fb) { fetching = fb; readable = true; }
                }
                catch { readable = false; }

                if (!readable) break;
                if (fetching) sawFetching = true;
                if (sawFetching && !fetching)
                {
                    settled++;
                    if (settled >= 24) break;
                }
                yield return null;
            }

            try
            {
                if (toggleMethod != null && viewInst != null && sectionsEnumMembers != null)
                {
                    object cs = GetPrivateField(viewInst, "currentSection");
                    bool onMembers = cs != null && cs.ToString() == "MEMBERS";
                    if (!onMembers)
                    {
                        ParameterInfo[] tp = toggleMethod.GetParameters();
                        object[] targs = new object[tp.Length];
                        targs[0] = sectionsEnumMembers;
                        for (int i = 1; i < tp.Length; i++)
                            targs[i] = (tp[i].ParameterType == typeof(bool)) ? (object)true
                                      : (tp[i].HasDefaultValue ? tp[i].DefaultValue : null);
                        toggleMethod.Invoke(viewInst, targs);
                    }
                }
            }
            catch {  }

            for (int i = 0; i < 120; i++) yield return null;

            try
            {
                if (toggleMethod != null && viewInst != null && sectionsEnumMembers != null)
                {
                    object cs = GetPrivateField(viewInst, "currentSection");
                    bool onMembers = cs != null && cs.ToString() == "MEMBERS";
                    if (!onMembers)
                    {
                        ParameterInfo[] tp = toggleMethod.GetParameters();
                        object[] targs = new object[tp.Length];
                        targs[0] = sectionsEnumMembers;
                        for (int i = 1; i < tp.Length; i++)
                            targs[i] = (tp[i].ParameterType == typeof(bool)) ? (object)true
                                      : (tp[i].HasDefaultValue ? tp[i].DefaultValue : null);
                        toggleMethod.Invoke(viewInst, targs);
                    }
                }
            }
            catch {  }

            for (int i = 0; i < 24; i++) yield return null;

            yield return CaptureShot("communitymembers");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_communitycontent(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_communitycontent", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            string err = null;
            object communitiesDataProvider = null;
            object listTask = null;

            try
            {
                object browser = FindControllerByTypeName(mvcManager, "CommunitiesBrowserController");
                if (browser != null) communitiesDataProvider = GetPrivateField(browser, "dataProvider");
                if (communitiesDataProvider == null)
                {
                    object card = FindControllerByTypeName(mvcManager, "CommunityCardController");
                    if (card != null) communitiesDataProvider = GetPrivateField(card, "communitiesDataProvider");
                }

                if (communitiesDataProvider != null)
                {

                    MethodInfo getCommunities = communitiesDataProvider.GetType()
                        .GetMethod("GetUserCommunitiesAsync", BindingFlags.Public | BindingFlags.Instance);
                    if (getCommunities != null)
                    {
                        ParameterInfo[] ps = getCommunities.GetParameters();
                        object[] args = new object[ps.Length];
                        args[0] = "";
                        args[1] = false;
                        args[2] = 1;
                        args[3] = 10;
                        args[4] = System.Threading.CancellationToken.None;
                        for (int i = 5; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                        listTask = getCommunities.Invoke(communitiesDataProvider, args);
                    }
                }
            }
            catch (System.Exception e) { err = "communitycontent: provider lookup: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield return CaptureShot("communitycontent"); yield break; }

            string communityId = null;
            if (listTask != null)
            {
                yield return AwaitUniTask(listTask);
                if (awaitedError == null && awaitedResult != null)
                {

                    try
                    {
                        object data = GetMember(awaitedResult, "data");
                        object results = data != null ? GetMember(data, "results") : null;
                        if (results is System.Collections.IEnumerable en)
                            foreach (object c in en)
                            {
                                object id = GetMember(c, "id");
                                if (id != null && !string.IsNullOrEmpty(id.ToString())) { communityId = id.ToString(); break; }
                            }
                    }
                    catch (System.Exception e) { err = "communitycontent: parse list: " + (e.InnerException?.Message ?? e.Message); }
                }
                if (err != null) { m.error = err; yield return CaptureShot("communitycontent"); yield break; }
            }

            if (string.IsNullOrEmpty(communityId))
            {
                m.error = "skipped:no-community-available (data-gated: no community id from GetUserCommunitiesAsync)";
                for (int i = 0; i < 18; i++) yield return null;
                yield return CaptureShot("communitycontent");
                yield break;
            }

            object param = null;
            try
            {
                Type paramType = FindType("DCL.Communities.CommunitiesCard.CommunityCardParameter");
                if (paramType == null) err = "communitycontent: CommunityCardParameter type not found";
                else
                {
                    ConstructorInfo ctor = null;
                    foreach (ConstructorInfo c in paramType.GetConstructors())
                        if (c.GetParameters().Length >= 1 && c.GetParameters()[0].ParameterType == typeof(string)) { ctor = c; break; }
                    if (ctor == null) err = "communitycontent: CommunityCardParameter(string,...) ctor not found";
                    else
                    {
                        ParameterInfo[] cp = ctor.GetParameters();
                        object[] cargs = new object[cp.Length];
                        cargs[0] = communityId;
                        for (int i = 1; i < cp.Length; i++) cargs[i] = cp[i].HasDefaultValue ? cp[i].DefaultValue : null;
                        param = ctor.Invoke(cargs);
                    }
                }
            }
            catch (System.Exception e) { err = "communitycontent: build param: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield return CaptureShot("communitycontent"); yield break; }

            if (!TryShowPanelByName(mvcManager, "DCL.Communities.CommunitiesCard.CommunityCardController", param, out string showErr))
            {
                m.error = "communitycontent: show panel failed: " + showErr;
                yield return CaptureShot("communitycontent");
                yield break;
            }

            object cardView = null;
            try
            {
                object controller = FindControllerByTypeName(mvcManager, "CommunityCardController");
                for (Type t = controller?.GetType(); t != null && cardView == null; t = t.BaseType)
                {
                    PropertyInfo vp = t.GetProperty("viewInstance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (vp != null) cardView = vp.GetValue(controller);
                }
            }
            catch (System.Exception) { cardView = null; }

            bool headerLoaded = false;
            for (int i = 0; i < 360 && !headerLoaded; i++)
            {
                try
                {
                    if (cardView != null)
                    {

                        object nameText = null;
                        PropertyInfo nameProp = cardView.GetType().GetProperty("communityName",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (nameProp != null) nameText = nameProp.GetValue(cardView);
                        if (nameText == null)
                        {
                            FieldInfo nameField = cardView.GetType().GetField("<communityName>k__BackingField",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            if (nameField != null) nameText = nameField.GetValue(cardView);
                        }
                        if (nameText != null)
                        {
                            string txt = nameText.GetType().GetProperty("text")?.GetValue(nameText) as string;
                            object enabledObj = nameText.GetType().GetProperty("enabled")?.GetValue(nameText);
                            bool enabled = enabledObj is bool b && b;
                            if (enabled && !string.IsNullOrEmpty(txt)) headerLoaded = true;
                        }
                    }
                }
                catch (System.Exception) {  }
                if (!headerLoaded) yield return null;
            }

            for (int i = 0; i < 24; i++) yield return null;

            try
            {
                Type sectionsEnum = FindType("DCL.Communities.CommunitiesCard.CommunityCardView+Sections");
                if (cardView != null && sectionsEnum != null)
                {
                    object placesSection = System.Enum.Parse(sectionsEnum, "PLACES");
                    MethodInfo toggle = cardView.GetType().GetMethod("ToggleSection",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new[] { sectionsEnum, typeof(bool) }, null);
                    if (toggle != null)
                        toggle.Invoke(cardView, new object[] { placesSection, true });
                }
            }
            catch (System.Exception) {  }

            for (int i = 0; i < 90; i++) yield return null;
            yield return CaptureShot("communitycontent");
            m.error = headerLoaded ? "shown" : "shown:degraded-header-load-timeout";
        }

        private static IEnumerator AtlasCapture_passportphotos(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_passportphotos", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            string err = null;
            object selfProfile = null;
            string userId = null;
            object profileTask = null;

            try
            {

                selfProfile = ReachSelfProfile(dynamicContainer);
                if (selfProfile != null)
                {
                    object ownProfile = GetPublicProperty(selfProfile, "OwnProfile");
                    if (ownProfile != null)
                        userId = GetPublicProperty(ownProfile, "UserId") as string;

                    if (string.IsNullOrEmpty(userId))
                    {
                        MethodInfo profileAsync = selfProfile.GetType()
                            .GetMethod("ProfileAsync", BindingFlags.Public | BindingFlags.Instance);
                        if (profileAsync != null)
                            profileTask = profileAsync.Invoke(selfProfile, new object[] { System.Threading.CancellationToken.None });
                    }
                }
            }
            catch (System.Exception e) { err = "passportphotos: identity-setup: " + (e.InnerException?.Message ?? e.Message); }

            if (string.IsNullOrEmpty(userId) && profileTask != null)
            {
                yield return AwaitUniTask(profileTask);
                if (awaitedError == null && awaitedResult != null)
                    userId = GetPublicProperty(awaitedResult, "UserId") as string;
            }

            bool opened = false;
            string openErr = null;
            try
            {
                Type paramT = FindType("DCL.Passport.PassportParams");
                if (paramT != null && !string.IsNullOrEmpty(userId))
                {

                    object param = Activator.CreateInstance(paramT, userId, null, true);
                    opened = TryShowPanelByName(mvcManager, "DCL.Passport.PassportController", param, out openErr);
                }
                else if (paramT == null)
                    openErr = "PassportParams type not found";
                else
                    openErr = "own userId unavailable";
            }
            catch (System.Exception e) { openErr = e.InnerException?.Message ?? e.Message; }
            if (openErr != null) err = (err == null ? "passportphotos: open: " + openErr : err + "; open: " + openErr);

            for (int i = 0; i < 18; i++) yield return null;

            try
            {
                object controller = FindControllerByTypeName(mvcManager, "PassportController");
                if (controller != null)
                {
                    MethodInfo openPhotos = controller.GetType()
                        .GetMethod("OpenPhotosSection", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (openPhotos != null)
                        openPhotos.Invoke(controller, null);

                }
            }
            catch (System.Exception e)
            {
                string phErr = e.InnerException?.Message ?? e.Message;
                err = (err == null ? "passportphotos: photos-section: " + phErr : err + "; photos-section: " + phErr);
            }

            for (int i = 0; i < 40; i++) yield return null;

            yield return CaptureShot("passportphotos");

            m.error = err == null ? "shown" : ("shown; " + err);
        }

        private static IEnumerator AtlasCapture_addlink(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_addlink", ok = true };
            report.actions.Add(m);

            string err = null;
            object selfProfile = null;
            string userId = null;
            object profileTask = null;
            try
            {
                selfProfile = ReachSelfProfile(dynamicContainer);
                if (selfProfile != null)
                {
                    object ownProfile = GetPublicProperty(selfProfile, "OwnProfile");
                    if (ownProfile != null)
                        userId = GetPublicProperty(ownProfile, "UserId") as string;

                    if (string.IsNullOrEmpty(userId))
                    {
                        MethodInfo profileAsync = selfProfile.GetType()
                            .GetMethod("ProfileAsync", BindingFlags.Public | BindingFlags.Instance);
                        if (profileAsync != null)
                            profileTask = profileAsync.Invoke(selfProfile, new object[] { System.Threading.CancellationToken.None });
                    }
                }
            }
            catch (System.Exception e) { err = "addlink: identity-setup: " + (e.InnerException?.Message ?? e.Message); }

            if (string.IsNullOrEmpty(userId) && profileTask != null)
            {
                yield return AwaitUniTask(profileTask);
                if (awaitedError == null && awaitedResult != null)
                    userId = GetPublicProperty(awaitedResult, "UserId") as string;
            }

            string openErr = null;
            try
            {
                Type paramT = FindType("DCL.Passport.PassportParams");
                if (paramT != null && !string.IsNullOrEmpty(userId))
                {

                    object param = Activator.CreateInstance(paramT, userId, null, true);
                    TryShowPanelByName(mvcManager, "DCL.Passport.PassportController", param, out openErr);
                }
                else if (paramT == null)
                    openErr = "PassportParams type not found";
                else
                    openErr = "own userId unavailable";
            }
            catch (System.Exception e) { openErr = e.InnerException?.Message ?? e.Message; }
            if (openErr != null) err = (err == null ? "addlink: open: " + openErr : err + "; open: " + openErr);

            for (int i = 0; i < 40; i++) yield return null;

            bool modalShown = false;
            string modalErr = null;
            try
            {
                object controller = FindControllerByTypeName(mvcManager, "PassportController");
                object viewInstance = controller != null ? GetMember(controller, "viewInstance") : null;
                if (viewInstance == null)
                    modalErr = "PassportView viewInstance null";
                else
                {

                    try
                    {
                        object detailView = GetPublicProperty(viewInstance, "UserDetailedInfoModuleView");
                        object editBtn = detailView != null ? GetPublicProperty(detailView, "LinksEditionButton") : null;
                        if (editBtn != null)
                        {
                            object onClick = GetMember(editBtn, "onClick");
                            if (onClick != null)
                            {
                                MethodInfo invoke = onClick.GetType().GetMethod("Invoke", new Type[0]);
                                if (invoke != null) invoke.Invoke(onClick, null);
                            }
                        }
                    }
                    catch {  }

                    object addLinkModal = GetPublicProperty(viewInstance, "AddLinkModal");
                    if (addLinkModal == null)
                        modalErr = "AddLinkModal null on view";
                    else
                    {
                        MethodInfo show = addLinkModal.GetType()
                            .GetMethod("Show", BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null);
                        if (show == null)
                            modalErr = "AddLink_PassportModal.Show() not found";
                        else
                        {
                            show.Invoke(addLinkModal, null);
                            modalShown = true;
                        }
                    }
                }
            }
            catch (System.Exception e) { modalErr = e.InnerException?.Message ?? e.Message; }
            if (modalErr != null) err = (err == null ? "addlink: modal: " + modalErr : err + "; modal: " + modalErr);

            for (int i = 0; i < 18; i++) yield return null;

            yield return CaptureShot("addlink");

            m.error = modalShown ? "shown" : ("shown; " + (err ?? "modal-not-shown"));
        }

        private static IEnumerator AtlasCapture_communitystream(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_communitystream", ok = true };
            report.actions.Add(m);
            yield return HideExplorePanel(mvcManager);

            string err = null;
            object communitiesDataProvider = null;
            object listTask = null;
            try
            {
                if (mvcManager == null) { err = "communitystream: mvcManager null"; }
                else
                {
                    object cardController = FindControllerByTypeName(mvcManager, "CommunityCardController");
                    if (cardController == null) err = "communitystream: CommunityCardController not registered";
                    else
                    {
                        communitiesDataProvider = GetPrivateField(cardController, "communitiesDataProvider");
                        if (communitiesDataProvider == null) err = "communitystream: communitiesDataProvider field not found";
                        else
                        {

                            MethodInfo getCommunities = communitiesDataProvider.GetType()
                                .GetMethod("GetUserCommunitiesAsync", BindingFlags.Public | BindingFlags.Instance);
                            if (getCommunities == null) err = "communitystream: GetUserCommunitiesAsync not found";
                            else
                            {
                                ParameterInfo[] ps = getCommunities.GetParameters();
                                object[] args = new object[ps.Length];
                                for (int i = 0; i < ps.Length; i++)
                                {
                                    if (ps[i].ParameterType == typeof(string)) args[i] = "";
                                    else if (ps[i].ParameterType == typeof(bool)) args[i] = (ps[i].Name == "isStreaming");
                                    else if (ps[i].ParameterType == typeof(int)) args[i] = (ps[i].Name == "pageNumber") ? 1 : 10;
                                    else if (ps[i].ParameterType == typeof(System.Threading.CancellationToken)) args[i] = System.Threading.CancellationToken.None;
                                    else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                                }
                                listTask = getCommunities.Invoke(communitiesDataProvider, args);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "communitystream: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }
            if (listTask == null) { m.error = "communitystream: list task null"; yield break; }

            yield return AwaitUniTask(listTask);
            if (awaitedError != null) { m.error = "communitystream: list fetch: " + awaitedError; yield break; }

            string communityId = null;
            try
            {
                object data = awaitedResult != null ? GetMember(awaitedResult, "data") : null;
                object results = data != null ? GetMember(data, "results") : null;
                if (results is System.Collections.IEnumerable enumerable)
                    foreach (object community in enumerable)
                    {
                        string ids = GetMember(community, "id")?.ToString();
                        if (!string.IsNullOrEmpty(ids)) { communityId = ids; break; }
                    }
            }
            catch (System.Exception e) { err = "communitystream: id extract: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }
            if (string.IsNullOrEmpty(communityId))
            {

                for (int i = 0; i < 18; i++) yield return null;
                yield return CaptureShot("communitystream");
                m.error = "communitystream: skipped:no-community-id (no streaming/member community for this account)";
                yield break;
            }

            try
            {
                Type controllerType = FindType("DCL.Communities.CommunitiesCard.CommunityCardController");
                Type paramType = FindType("DCL.Communities.CommunitiesCard.CommunityCardParameter");
                if (controllerType == null || paramType == null) err = "communitystream: card types not found";
                else
                {
                    object param = System.Activator.CreateInstance(paramType, communityId, null);
                    MethodInfo issueCommand = controllerType.GetMethod("IssueCommand",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                        null, new[] { paramType }, null);
                    if (issueCommand == null) err = "communitystream: IssueCommand(param) not found";
                    else
                    {
                        object command = issueCommand.Invoke(null, new[] { param });
                        if (command == null) err = "communitystream: IssueCommand returned null";
                        else
                        {
                            MethodInfo showAsync = null;
                            foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                            if (showAsync == null) err = "communitystream: ShowAsync not found";
                            else
                            {
                                Type[] genArgs = command.GetType().GetGenericArguments();
                                showAsync.MakeGenericMethod(genArgs)
                                    .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });
                                Type ifaceOpen = FindType("MVC.IController`2");
                                lastPanelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "communitystream: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 210; i++) yield return null;
            if (lastPanelKey != null) VerifyShown(mvcManager, lastPanelKey, out _);
            yield return CaptureShot("communitystream");
            m.error = "shown";
        }

        private static IEnumerator AtlasCapture_broadcast(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_broadcast", ok = true };
            report.actions.Add(m);

            string err = null;
            object dataProvider = null;
            object orchestrator = null;
            object listTask = null;
            Type orchestratorIface = null;

            try
            {
                if (mvcManager == null) { err = "broadcast: mvcManager null"; }
                else
                {
                    orchestratorIface = FindType("DCL.VoiceChat.IVoiceChatOrchestrator");

                    object dict = GetPublicProperty(mvcManager, "Controllers");
                    if (dict == null)
                    {
                        object core = GetPrivateField(mvcManager, "core");
                        if (core != null) dict = GetPublicProperty(core, "Controllers");
                    }
                    object values = dict != null ? GetPublicProperty(dict, "Values") : null;
                    if (values is System.Collections.IEnumerable en)
                    {
                        foreach (object ctrl in en)
                        {
                            if (ctrl == null) continue;
                            foreach (FieldInfo fi in ctrl.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                            {
                                object fv = fi.GetValue(ctrl);
                                if (fv == null) continue;

                                if (dataProvider == null && fv.GetType().Name == "CommunitiesDataProvider")
                                    dataProvider = fv;

                                if (orchestrator == null && orchestratorIface != null && orchestratorIface.IsInstanceOfType(fv))
                                    orchestrator = fv;

                                if (orchestrator == null && orchestratorIface != null && fv.GetType().Name.Contains("Presenter"))
                                {
                                    foreach (FieldInfo sfi in fv.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                                    {
                                        object sfv = sfi.GetValue(fv);
                                        if (sfv != null && orchestratorIface.IsInstanceOfType(sfv)) { orchestrator = sfv; break; }
                                    }
                                }
                            }
                            if (dataProvider != null && orchestrator != null) break;
                        }
                    }

                    if (dataProvider == null)
                        err = "broadcast: skipped:no-CommunitiesDataProvider-reachable (no registered controller holds it yet)";
                    else
                    {

                        var ct = System.Threading.CancellationToken.None;
                        MethodInfo mi = null;
                        foreach (MethodInfo c in dataProvider.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (c.Name == "GetUserCommunitiesAsync") { mi = c; break; }
                        if (mi == null) err = "broadcast: GetUserCommunitiesAsync not found";
                        else
                        {
                            ParameterInfo[] ps = mi.GetParameters();
                            object[] args = new object[ps.Length];
                            for (int i = 0; i < ps.Length; i++)
                            {
                                if (ps[i].ParameterType == typeof(string)) args[i] = "";
                                else if (ps[i].ParameterType == typeof(bool)) args[i] = (ps[i].Name == "onlyMemberOf");
                                else if (ps[i].ParameterType == typeof(int)) args[i] = (ps[i].Name == "pageNumber") ? 1 : 20;
                                else if (ps[i].ParameterType == typeof(System.Threading.CancellationToken)) args[i] = ct;
                                else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                            }
                            listTask = mi.Invoke(dataProvider, args);
                            if (listTask == null) err = "broadcast: GetUserCommunitiesAsync returned null";
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "broadcast: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            yield return AwaitUniTask(listTask);
            if (awaitedError != null) { m.error = "broadcast: list-failed: " + awaitedError; yield break; }

            string communityId = null;
            string ownedNote = "any-community (no owned row found)";
            try
            {
                object resp = awaitedResult;
                object payload = resp;
                object maybeValue = resp != null ? GetMember(resp, "Value") : null;
                if (maybeValue != null && maybeValue.GetType().Name.Contains("Response")) payload = maybeValue;

                object data = payload != null ? GetMember(payload, "data") : null;
                object results = data != null ? GetMember(data, "results") : null;

                string firstAny = null;
                if (results is System.Collections.IEnumerable ren)
                {
                    foreach (object comm in ren)
                    {
                        string id = GetMember(comm, "id") as string;
                        if (string.IsNullOrEmpty(id)) continue;
                        if (firstAny == null) firstAny = id;

                        object roleObj = GetMember(comm, "role");
                        if (roleObj != null && string.Equals(roleObj.ToString(), "owner", System.StringComparison.OrdinalIgnoreCase))
                        {
                            communityId = id;
                            ownedNote = "owned-community";
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(communityId)) communityId = firstAny;

                if (string.IsNullOrEmpty(communityId))
                    err = "broadcast: dataGated:no-community-in-results (cannot go live without a community)";
            }
            catch (System.Exception e) { err = "broadcast: pick-community: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            bool fired = false;
            try
            {
                if (orchestrator == null)
                {
                    err = "broadcast: skipped-mutation:no-IVoiceChatOrchestrator-reachable (capturing UI only)";
                }
                else
                {
                    Type vcTypeEnum = FindType("DCL.VoiceChat.VoiceChatType");
                    MethodInfo startCall = null;
                    foreach (MethodInfo mi in orchestratorIface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                        if (mi.Name == "StartCall" && mi.GetParameters().Length == 2) { startCall = mi; break; }
                    if (startCall == null) startCall = orchestrator.GetType().GetMethod("StartCall",
                        BindingFlags.Public | BindingFlags.Instance, null,
                        new[] { typeof(string), vcTypeEnum }, null);

                    if (startCall == null) err = "broadcast: StartCall(string,VoiceChatType) not found";
                    else if (vcTypeEnum == null) err = "broadcast: VoiceChatType enum not found";
                    else
                    {
                        object communityVal = System.Enum.Parse(vcTypeEnum, "COMMUNITY");

                        startCall.Invoke(orchestrator, new object[] { communityId, communityVal });
                        fired = true;
                    }
                }
            }
            catch (System.Exception e) { err = "broadcast: StartCall failed: " + (e.InnerException?.Message ?? e.Message); }

            string liveState = "unknown";
            if (fired && orchestrator != null)
            {
                object callStatusProp = null;
                try { callStatusProp = GetMember(orchestrator, "CommunityCallStatus"); } catch { callStatusProp = null; }

                for (int i = 0; i < 240; i++)
                {
                    string sv = null;
                    try
                    {
                        object val = callStatusProp != null ? GetMember(callStatusProp, "Value") : null;
                        if (val != null) sv = val.ToString();
                    }
                    catch { sv = null; }
                    if (sv != null) liveState = sv;
                    if (sv == "VOICE_CHAT_IN_CALL") break;
                    yield return null;
                }
            }

            try
            {
                Type viewType = FindType("DCL.VoiceChat.CommunityVoiceChat.CommunityVoiceChatPanelView");
                if (viewType != null)
                {
                    var found = UnityEngine.Object.FindObjectsByType(viewType, FindObjectsInactive.Include);
                    if (found != null && found.Length > 0 && found[0] is Component pvComp && pvComp != null)
                    {
                        for (Transform tr = pvComp.transform; tr != null; tr = tr.parent)
                            if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);

                        var showM = viewType.GetMethod("Show", BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                        if (showM != null) showM.Invoke(found[0], null);
                    }
                }
            }
            catch {  }

            for (int i = 0; i < 24; i++) yield return null;
            yield return CaptureShot("communitystream");

            if (err != null)
                m.error = err + " [community=" + ownedNote + ", liveState=" + liveState + "] (captured fallback)";
            else if (liveState == "VOICE_CHAT_IN_CALL")
                m.error = "shown";
            else
                m.error = "shown (mutation fired; liveState=" + liveState + ", community=" + ownedNote +
                          " — stream may need mic permission or backend ownership to reach IN_CALL)";
        }

        private static IEnumerator AtlasCapture_contextmenu(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_contextmenu", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            string err = null;
            Type panelKey = null;

            try
            {

                Type menuT = FindType("DCL.UI.GenericContextMenu");
                Type paramT = FindType("DCL.UI.GenericContextMenuParameter");
                Type controllerT = FindType("DCL.UI.GenericContextMenuController");
                Type textSettingsT = FindType("DCL.UI.Controls.Configs.TextContextMenuControlSettings");
                Type buttonSettingsT = FindType("DCL.UI.Controls.Configs.SimpleButtonContextMenuControlSettings");
                Type separatorSettingsT = FindType("DCL.UI.Controls.Configs.SeparatorContextMenuControlSettings");

                if (menuT == null) err = "contextmenu: GenericContextMenu type not found";
                else if (paramT == null) err = "contextmenu: GenericContextMenuParameter type not found";
                else if (controllerT == null) err = "contextmenu: GenericContextMenuController type not found";
                else if (textSettingsT == null) err = "contextmenu: TextContextMenuControlSettings type not found";
                else if (buttonSettingsT == null) err = "contextmenu: SimpleButtonContextMenuControlSettings type not found";
                else if (separatorSettingsT == null) err = "contextmenu: SeparatorContextMenuControlSettings type not found";

                object menuConfig = null;
                if (err == null)
                {
                    menuConfig = CreateContextMenuOptional(menuT, new object[0]);
                    if (menuConfig == null) err = "contextmenu: could not instantiate GenericContextMenu";
                }

                if (err == null)
                {
                    MethodInfo addControl = null;
                    foreach (MethodInfo mi in menuT.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (mi.Name != "AddControl") continue;
                        ParameterInfo[] ps = mi.GetParameters();

                        if (ps.Length == 1 && ps[0].ParameterType.Name == "IContextMenuControlSettings") { addControl = mi; break; }
                    }
                    if (addControl == null) err = "contextmenu: AddControl(IContextMenuControlSettings) not found";

                    if (err == null)
                    {
                        System.Action noop = () => { };

                        var redColor = new UnityEngine.Color(1f, 0.176f, 0.333f, 1f);

                        object header = CreateContextMenuOptional(textSettingsT, new object[] { "Alice.dcl" });
                        object sep1 = CreateContextMenuOptional(separatorSettingsT, new object[0]);

                        object btnMention = CreateContextMenuOptional(buttonSettingsT, new object[] { "Mention", noop });
                        object btnProfile = CreateContextMenuOptional(buttonSettingsT, new object[] { "View Profile", noop });
                        object btnChat = CreateContextMenuOptional(buttonSettingsT, new object[] { "Chat", noop });
                        object btnCall = CreateContextMenuOptional(buttonSettingsT, new object[] { "Call", noop });
                        object btnGift = CreateContextMenuOptional(buttonSettingsT, new object[] { "Gift", noop });
                        object btnJump = CreateContextMenuOptional(buttonSettingsT, new object[] { "Jump to Location", noop });

                        object sep2 = CreateContextMenuOptional(separatorSettingsT, new object[0]);

                        object btnReport = CreateContextMenuOptional(buttonSettingsT,
                            new object[] { "Report", noop, null, 10, false, redColor });
                        object btnBlock = CreateContextMenuOptional(buttonSettingsT,
                            new object[] { "Block", noop, null, 10, false, redColor });

                        if (btnReport == null) btnReport = CreateContextMenuOptional(buttonSettingsT, new object[] { "Report", noop });
                        if (btnBlock == null) btnBlock = CreateContextMenuOptional(buttonSettingsT, new object[] { "Block", noop });

                        object[] controls = { header, sep1, btnMention, btnProfile, btnChat, btnCall, btnGift,
                                              btnJump, sep2, btnReport, btnBlock };

                        bool anyNull = false;
                        foreach (object c in controls) if (c == null) anyNull = true;

                        if (anyNull)
                            err = "contextmenu: failed to build one or more context-menu control settings";
                        else
                            foreach (object c in controls)
                                addControl.Invoke(menuConfig, new object[] { c });
                    }
                }

                object param = null;
                if (err == null)
                {
                    var anchor = new UnityEngine.Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

                    ConstructorInfo paramCtor = null;
                    foreach (ConstructorInfo ci in paramT.GetConstructors())
                        if (ci.GetParameters().Length >= 2) { paramCtor = ci; break; }
                    if (paramCtor == null) err = "contextmenu: GenericContextMenuParameter ctor not found";

                    if (err == null)
                    {
                        ParameterInfo[] cps = paramCtor.GetParameters();
                        object[] args = new object[cps.Length];
                        args[0] = menuConfig;
                        args[1] = anchor;

                        for (int i = 2; i < cps.Length; i++) args[i] = Type.Missing;
                        param = paramCtor.Invoke(args);
                    }
                }

                object command = null;
                if (err == null)
                {
                    MethodInfo issue = null;
                    foreach (MethodInfo mi in controllerT.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                        if (mi.Name == "IssueCommand" && mi.GetParameters().Length == 1) { issue = mi; break; }
                    if (issue == null) err = "contextmenu: IssueCommand(1-arg) not found";

                    command = issue != null ? issue.Invoke(null, new object[] { param }) : null;
                    if (err == null && command == null) err = "contextmenu: IssueCommand returned null";
                }

                if (err == null)
                {
                    MethodInfo showAsync = null;
                    foreach (MethodInfo mi in mvcManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        if (mi.Name == "ShowAsync" && mi.IsGenericMethodDefinition) { showAsync = mi; break; }
                    if (showAsync == null) err = "contextmenu: ShowAsync not found";

                    if (err == null)
                    {
                        Type[] genArgs = command.GetType().GetGenericArguments();
                        showAsync.MakeGenericMethod(genArgs)
                                 .Invoke(mvcManager, new object[] { command, System.Threading.CancellationToken.None });

                        Type ifaceOpen = FindType("MVC.IController`2");
                        panelKey = ifaceOpen != null ? ifaceOpen.MakeGenericType(genArgs) : null;
                    }
                }
            }
            catch (System.Exception e) { err = "contextmenu: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 20; i++) yield return null;

            if (panelKey != null && !VerifyShown(mvcManager, panelKey, out string verifyErr))
                m.error = "contextmenu: " + verifyErr;

            yield return CaptureShot("contextmenu");

            if (m.error == null)
                m.error = "shown";
        }

        private static object CreateContextMenuOptional(Type t, object[] args)
        {
            foreach (ConstructorInfo ci in t.GetConstructors())
            {
                ParameterInfo[] ps = ci.GetParameters();
                if (ps.Length < args.Length) continue;
                object[] full = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++) full[i] = i < args.Length ? args[i] : Type.Missing;
                try { return ci.Invoke(full); } catch {  }
            }
            return null;
        }

        private static IEnumerator AtlasCapture_confirm(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_confirm", ok = true };
            report.actions.Add(m);

            yield return HideExplorePanel(mvcManager);

            string err = null;
            object param = null;
            try
            {
                if (mvcManager == null) { err = "confirm: mvcManager is null"; }
                else
                {

                    Type paramT = FindType("DCL.UI.ConfirmationDialog.Opener.ConfirmationDialogParameter");
                    if (paramT == null) { err = "confirm: ConfirmationDialogParameter type not found"; }
                    else
                    {
                        ConstructorInfo[] ctors = paramT.GetConstructors();
                        ConstructorInfo ctor = null;
                        for (int i = 0; i < ctors.Length; i++)
                            if (ctors[i].GetParameters().Length >= 6) { ctor = ctors[i]; break; }
                        if (ctor == null) { err = "confirm: matching ctor not found"; }
                        else
                        {
                            ParameterInfo[] ps = ctor.GetParameters();
                            object[] args = new object[ps.Length];
                            args[0] = "Are you sure?";
                            args[1] = "Cancel";
                            args[2] = "Confirm";
                            args[3] = null;
                            args[4] = false;
                            args[5] = false;
                            for (int i = 6; i < ps.Length; i++) args[i] = Type.Missing;
                            param = ctor.Invoke(BindingFlags.OptionalParamBinding, null, args, null);
                        }
                    }
                }
            }
            catch (System.Exception e) { err = "confirm: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = "not-shown: " + err; yield break; }

            string showErr;
            bool opened = TryShowPanelByName(mvcManager, "DCL.UI.ConfirmationDialog.ConfirmationDialogController", param, out showErr);
            if (!opened) { m.error = "not-shown: confirm: show-failed (" + showErr + ")"; yield break; }

            for (int i = 0; i < 18; i++) yield return null;
            yield return CaptureShot("confirm");

            string rerr = null;
            try { if (lastPanelKey != null && !VerifyShown(mvcManager, lastPanelKey, out rerr)) rerr = "not-shown: " + rerr; else rerr = null; }
            catch (System.Exception e) { rerr = "verify-failed: " + (e.InnerException?.Message ?? e.Message); }
            m.error = rerr ?? "shown";
        }

        private static IEnumerator AtlasCapture_gallery(object mvcManager, object staticContainer, object dynamicContainer, object realmNavigator, Report report)
        {
            var m = new PhaseMarker { label = "atlas_gallery", ok = true };
            report.actions.Add(m);

            string err = null;
            try
            {
                if (mvcManager == null) err = "gallery: mvcManager null";
                else if (!TryOpenExplorePanel(mvcManager, "CameraReel", null, out string openErr))
                    err = "gallery: open CameraReel section: " + openErr;

            }
            catch (System.Exception e) { err = "gallery: " + (e.InnerException?.Message ?? e.Message); }
            if (err != null) { m.error = err; yield break; }

            for (int i = 0; i < 18; i++) yield return null;

            try
            {
                object explorePanelCtl = FindControllerByTypeName(mvcManager, "ExplorePanelController");
                if (explorePanelCtl != null)
                {
                    object cameraReelController = GetMember(explorePanelCtl, "CameraReelController");
                    if (cameraReelController != null)
                    {
                        MethodInfo activate = cameraReelController.GetType()
                            .GetMethod("Activate", BindingFlags.Public | BindingFlags.Instance);
                        activate?.Invoke(cameraReelController, null);
                    }
                }
            }
            catch (System.Exception e)
            {
                string aErr = e.InnerException?.Message ?? e.Message;
                err = (err == null ? "gallery: activate: " + aErr : err + "; activate: " + aErr);
            }

            for (int i = 0; i < 45; i++) yield return null;

            if (lastPanelKey != null) VerifyShown(mvcManager, lastPanelKey, out _);
            yield return CaptureShot("gallery");
            m.error = err == null ? "shown" : ("shown; " + err);
        }

        private class Report
        {
            public string startedUtc, finishedUtc;
            public double totalWallSeconds;
            public string fatal;
            public bool reachedInteractive;
            public float timeToInteractiveSeconds;
            public string lastLoadingStage;
            public bool foundRealmNavigator, foundChatBus, foundProfiler;
            public int totalLogMessages, warningCount, errorCount;
            public List<PhaseMetrics> phases = new();
            public List<PhaseMarker>  actions = new();
            public List<LogEntry> warnings = new();
            public List<LogEntry> errors = new();

            public string ToJson()
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                J(sb, "startedUtc", startedUtc); J(sb, "finishedUtc", finishedUtc);
                Jn(sb, "totalWallSeconds", totalWallSeconds);
                if (fatal != null) J(sb, "fatal", fatal);
                Jb(sb, "reachedInteractive", reachedInteractive);
                Jn(sb, "timeToInteractiveSeconds", timeToInteractiveSeconds);
                J(sb, "lastLoadingStage", lastLoadingStage);
                Jb(sb, "foundRealmNavigator", foundRealmNavigator);
                Jb(sb, "foundChatBus", foundChatBus);
                Jb(sb, "foundProfiler", foundProfiler);
                Jn(sb, "totalLogMessages", totalLogMessages);
                Jn(sb, "warningCount", warningCount);
                Jn(sb, "errorCount", errorCount);

                sb.Append("  \"phases\": [\n");
                for (int i = 0; i < phases.Count; i++) { sb.Append("    "); sb.Append(phases[i].ToJson()); sb.Append(i < phases.Count - 1 ? ",\n" : "\n"); }
                sb.Append("  ],\n");

                sb.Append("  \"actions\": [\n");
                for (int i = 0; i < actions.Count; i++) { sb.Append("    "); sb.Append(actions[i].ToJson()); sb.Append(i < actions.Count - 1 ? ",\n" : "\n"); }
                sb.Append("  ],\n");

                AppendLogs(sb, "errors", errors, true);
                AppendLogs(sb, "warnings", warnings, false);
                sb.Append("}\n");
                return sb.ToString();
            }

            private static void AppendLogs(StringBuilder sb, string key, List<LogEntry> logs, bool more)
            {
                sb.Append("  \"" + key + "\": [\n");
                for (int i = 0; i < logs.Count; i++)
                {
                    sb.Append("    {");
                    sb.Append("\"type\":\"" + Esc(logs[i].type) + "\",");
                    sb.Append("\"message\":\"" + Esc(logs[i].message) + "\"");
                    sb.Append("}");
                    sb.Append(i < logs.Count - 1 ? ",\n" : "\n");
                }
                sb.Append(more ? "  ],\n" : "  ]\n");
            }

            private static void J(StringBuilder sb, string k, string v) => sb.Append("  \"" + k + "\": " + (v == null ? "null" : "\"" + Esc(v) + "\"") + ",\n");
            private static void Jn(StringBuilder sb, string k, double v) => sb.Append("  \"" + k + "\": " + v.ToString(CultureInfo.InvariantCulture) + ",\n");
            private static void Jb(StringBuilder sb, string k, bool v) => sb.Append("  \"" + k + "\": " + (v ? "true" : "false") + ",\n");
        }

        private class PhaseMetrics
        {
            public string label;
            public int frames; public double durationSeconds;
            public double cpuMsAvg, cpuMsP99Worst, cpuMsMax, fpsAvg, gpuMsAvg, gpuMsMax;
            public long hiccupFramesOver50ms;
            public double gcAllocBytesTotal, systemUsedMemoryMB;
            public long drawCallsLast, batchesLast, setPassLast, trianglesLast;
            public string ToJson() =>
                "{" +
                $"\"label\":\"{Esc(label)}\",\"frames\":{frames},\"durationSeconds\":{durationSeconds.ToString(CultureInfo.InvariantCulture)}," +
                $"\"fpsAvg\":{fpsAvg.ToString("F2", CultureInfo.InvariantCulture)},\"cpuMsAvg\":{cpuMsAvg.ToString("F3", CultureInfo.InvariantCulture)}," +
                $"\"cpuMsP99Worst\":{cpuMsP99Worst.ToString("F3", CultureInfo.InvariantCulture)},\"cpuMsMax\":{cpuMsMax.ToString("F3", CultureInfo.InvariantCulture)}," +
                $"\"gpuMsAvg\":{gpuMsAvg.ToString("F3", CultureInfo.InvariantCulture)},\"gpuMsMax\":{gpuMsMax.ToString("F3", CultureInfo.InvariantCulture)}," +
                $"\"hiccupFramesOver50ms\":{hiccupFramesOver50ms},\"gcAllocBytesTotal\":{gcAllocBytesTotal.ToString("F0", CultureInfo.InvariantCulture)}," +
                $"\"systemUsedMemoryMB\":{systemUsedMemoryMB.ToString("F1", CultureInfo.InvariantCulture)}," +
                $"\"drawCallsLast\":{drawCallsLast},\"batchesLast\":{batchesLast},\"setPassLast\":{setPassLast},\"trianglesLast\":{trianglesLast}" +
                "}";
        }

        private class PhaseMarker
        {
            public string label; public bool ok; public string error;
            public string ToJson() => $"{{\"label\":\"{Esc(label)}\",\"ok\":{(ok ? "true" : "false")},\"error\":{(error == null ? "null" : "\"" + Esc(error) + "\"")}}}";
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            return sb.ToString();
        }
    }

    internal static class HarnessRunner
    {

        private static readonly List<Stack<IEnumerator>> stacks = new();
        private static bool hooked;

        public static void Start(IEnumerator routine)
        {
            var s = new Stack<IEnumerator>();
            s.Push(routine);
            stacks.Add(s);
            if (!hooked) { EditorApplication.update += Tick; hooked = true; }
        }

        private static void Tick()
        {
            for (int i = stacks.Count - 1; i >= 0; i--)
            {
                var stack = stacks[i];
                if (stack.Count == 0) { stacks.RemoveAt(i); continue; }
                IEnumerator top = stack.Peek();
                bool moved;
                try { moved = top.MoveNext(); }
                catch (Exception e) { Debug.LogError("[HarnessRunner] " + e); stacks.RemoveAt(i); continue; }
                if (moved)
                {
                    if (top.Current is IEnumerator nested) stack.Push(nested);

                }
                else
                {
                    stack.Pop();
                    if (stack.Count == 0) stacks.RemoveAt(i);
                }
            }
            if (stacks.Count == 0 && hooked) { EditorApplication.update -= Tick; hooked = false; }
        }
    }
}
#endif
