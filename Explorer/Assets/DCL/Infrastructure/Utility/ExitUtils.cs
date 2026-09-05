#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using DCL.Diagnostics;
using DCL.Prefs;
using UnityEngine;
using Utility.Multithreading;
using RichTypes;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Utility
{
    public class OnQuittingCleanUpCandidate
    {
        private readonly string name;
        private readonly Action callback;

        public string Name => name;

        public OnQuittingCleanUpCandidate(string name, Action callback)
        {
            this.name = name;
            this.callback = callback;
        }

        internal void Execute(Stopwatch stopwatch)
        {
            long startedAtMs = stopwatch.ElapsedMilliseconds;

            try
            {
                callback.Invoke();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UNSPECIFIED);
            }

            long elapsedMs = stopwatch.ElapsedMilliseconds - startedAtMs;
            ReportHub.LogProductionInfo($"[ExitUtils] '{name}' cleanup took {elapsedMs}ms (total {stopwatch.ElapsedMilliseconds}ms)");
        }
    }

    public static class ExitUtils
    {
        private static readonly Mutex<List<OnQuittingCleanUpCandidate>> candidates = new (new ()); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
        private static readonly Atomic<bool> isExiting = new (false);

        private static bool useSoftShutdown;
        private static bool useNativeShutdownStopwatch;

        /// <summary>
        ///     In the Editor always false so the full dispose path runs on Play Mode exit —
        ///     leaked native resources would accumulate in the Editor process until restart.
        /// </summary>
#if UNITY_EDITOR
        public static bool IsAboutToQuit => false;
#else
        public static bool IsAboutToQuit => isExiting.Value();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SubscribeToApplicationQuitting()
        {
#if UNITY_EDITOR
            Patch.Reset();

            // ensure isExiting is false on reopening
            isExiting.Set(false);
            using (var scope = candidates.Lock()) // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
                scope.Value.Clear();
#endif
            Application.quitting += OnApplicationQuitting;

            // Dirty reflection hack because there is no other way to view the subs of Application.quitting
            Patch.Apply();
        }

        private static void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;
            ReportHub.LogProductionInfo("[ExitUtils] triggered by Unity's Application.quitting");
            Exit();
        }


        public static void RegisterCleanUpCandidate(OnQuittingCleanUpCandidate candidate)
        {
            if (isExiting)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] Ignored RegisterCleanUpCandidate('{candidate.Name}') because Exit() is already in progress");
                return;
            }

            using var scope = candidates.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG

            for (var i = 0; i < scope.Value.Count; i++)
            {
                if (scope.Value[i].Name == candidate.Name)
                    throw new InvalidOperationException($"[ExitUtils] Cleanup candidate '{candidate.Name}' is already registered");
            }

            scope.Value.Add(candidate);
        }

        public static void UnregisterCleanUpCandidate(string name)
        {
            if (isExiting)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] Ignored UnregisterCleanUpCandidate('{name}') because Exit() is already in progress");
                return;
            }

            using var scope = candidates.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG

            for (var i = 0; i < scope.Value.Count; i++)
            {
                if (scope.Value[i].Name == name)
                {
                    scope.Value.RemoveAt(i);
                    return;
                }
            }
        }

        public static void Configure(bool softShutdown, bool nativeShutdownStopwatch)
        {
            useSoftShutdown = softShutdown;
            useNativeShutdownStopwatch = nativeShutdownStopwatch;
            ReportHub.LogProductionInfo($"[ExitUtils] Configured: softShutdown - {useSoftShutdown}, nativeShutdownStopwatch - {useNativeShutdownStopwatch}");
        }

        // Safe to call multiple times
        public static void Exit()
        {
            if (Cysharp.Threading.Tasks.PlayerLoopHelper.IsMainThread == false)
            {
                ReportHub.LogProductionInfo("[ExitUtils] Exit() cannot be called from not a main thread");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            ReportHub.LogProductionInfo($"[ExitUtils] Exit requested at {DateTime.UtcNow:O}");

            if (isExiting)
            {
                ReportHub.LogProductionInfo("[ExitUtils] Exit() ignored because it is already in progress");
                return;
            }

            isExiting.Set(true);

#if UNITY_STANDALONE_WIN
            if (useNativeShutdownStopwatch)
            {
                StartExitStopwatch();
            }
#endif

            using (var scope = candidates.Lock()) // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            {
                foreach (OnQuittingCleanUpCandidate candidate in scope.Value)
                    candidate.Execute(stopwatch);
            }

            ReportHub.LogProductionInfo($"[ExitUtils] CleanUpCandidates finished at {stopwatch.ElapsedMilliseconds}ms");

            // Flush save file only AFTER the candidates
            DCLPlayerPrefs.SaveSync();
            ReportHub.LogProductionInfo($"[ExitUtils] DCLPlayerPrefs flushed at {stopwatch.ElapsedMilliseconds}ms");

            // Reflection may drop the values. Reapply to be sure, and to move TryTerminateSelf back to the
            // last position in case anything subscribed to Application.quitting after the previous Apply().
            Patch.Apply();

            ReportHub.LogProductionInfo($"[ExitUtils] Begin Quit call {stopwatch.ElapsedMilliseconds}ms");
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
            ReportHub.LogProductionInfo($"[ExitUtils] Quit call dispatched at {stopwatch.ElapsedMilliseconds}ms");
        }

#if UNITY_STANDALONE_WIN
        // Expected to be called from main thread only.
        private static void StartExitStopwatch()
        {
            string targetPath = NewTargetPath();
            string exePath = StopwatchExePath();

            int pid = Process.GetCurrentProcess().Id; // IL2CPP safe

            // --target-pid <pid> -o <file>
            string[] args = new []
            {
                "--target-pid",
                pid.ToString(),
                "-o",
                targetPath 
            };

            ReportHub.LogProductionInfo($"[ExitUtils] Start measuring native exit delay");

            Result<int> result = Plugins.DclNativeProcesses.DclProcesses.Start(exePath, args);
            
            if (result.Success == false)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] Cannot start measuring native exit delay: {result.ErrorMessage}");
            }
        }

        private static string NewTargetPath()
        {
            string dir = UnityEngine.Application.persistentDataPath;
            long unixTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string name = $"exit_stopwatch_{unixTime}.log";
            string path = System.IO.Path.Combine(dir, name);
            return path;
        }

        private static string StopwatchExePath()
        {
            string dir = UnityEngine.Application.streamingAssetsPath;
            string path = System.IO.Path.Combine(dir, "dcl_exit_stopwatch.exe");
            return path;
        }
#endif

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        private static void TryTerminateSelf()
        {
            int pid = Process.GetCurrentProcess().Id; // IL2CPP safe
            ReportHub.LogProductionInfo($"[ExitUtils] Terminating process pid={pid}");

            if (useSoftShutdown)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] Terminating process aborted due useSoftShutdown mode");
                return;
            }

#if UNITY_STANDALONE_WIN
            IntPtr handle = OpenProcess(PROCESS_TERMINATE, false, (uint)pid);

            if (handle == IntPtr.Zero)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] OpenProcess failed (err {Marshal.GetLastWin32Error()})");
                return;
            }

            if (TerminateProcess(handle, 0) == false)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] TerminateProcess failed (err {Marshal.GetLastWin32Error()})");
                CloseHandle(handle);
            }
#elif UNITY_STANDALONE_OSX
            if (kill(pid, SIGKILL) != 0)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] kill(SIGKILL) failed (errno {Marshal.GetLastWin32Error()})");
            }
#endif
        }

#if UNITY_STANDALONE_WIN

        private const uint PROCESS_TERMINATE = 0x0001;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        
#elif UNITY_STANDALONE_OSX

        private const int SIGKILL = 9;

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);

#endif

#endif

        private static class Patch
        {
            private static readonly HashSet<Action> wrapped = new ();
            private static FieldInfo quittingField;
            
            // Safe to call multiple times.
            public static void Apply()
            {
                ApplicationQuittingFirstSubscriberSelfPatchWithTimers();
                RegisterTerminateSelf();
            }

            public static void Reset()
            {
                wrapped.Clear();
            }

            private static FieldInfo? QuitFieldInfo()
            {
                quittingField ??= typeof(Application).GetField("quitting", BindingFlags.NonPublic | BindingFlags.Static);

                if (quittingField == null)
                {
                    ReportHub.LogProductionInfo("[ExitUtils.Patch] Cannot find Application.quitting backing field");
                }

                return quittingField;
            }

            // Keeps TryTerminateSelf as the very LAST Application.quitting subscriber.
            // Safe to call multiple times.
            private static void RegisterTerminateSelf()
            {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
                try
                {
                    var quittingField = QuitFieldInfo();
                    if (quittingField == null)
                    {
                        return;
                    }

                    Action terminateSelf = TryTerminateSelf;

                    wrapped.Add(terminateSelf);

                    Action? current = quittingField.GetValue(null) as Action;
                    Delegate[] handlers = current?.GetInvocationList() ?? Array.Empty<Delegate>();

                    Action? rebuilt = null;

                    // Delete if exists
                    foreach (Delegate handler in handlers)
                    {
                        if (handler is not Action action) continue;
                        if (action == terminateSelf) continue;

                        rebuilt += action;
                    }

                    // Append at the very end
                    rebuilt += terminateSelf;

                    quittingField.SetValue(null, rebuilt);
                }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.UNSPECIFIED); }
#endif
            }

            // Method is safe to be called multiple times. Idempotency
            private static void ApplicationQuittingFirstSubscriberSelfPatchWithTimers()
            {
                ReportHub.LogProductionInfo($"[ExitUtils.Patch] Invoke ApplicationQuittingFirstSubscriberSelfPatchWithTimers, actions wrapped {wrapped.Count}"); 

                try
                {
                    var quittingField = QuitFieldInfo();
                    if (quittingField == null)
                    {
                        return;
                    }

                    Action? current = quittingField.GetValue(null) as Action;
                    Delegate[] handlers = current?.GetInvocationList() ?? Array.Empty<Delegate>();
                    ReportHub.LogProductionInfo($"[ExitUtils.Patch] Delegates count from the original Application.quitting: {handlers.Length}");

                    Action selfFirst = ApplicationQuittingFirstSubscriberSelfPatchWithTimers;
                    Action rebuilt = selfFirst;

                    foreach (Delegate handler in handlers)
                    {
                        if (handler is not Action action) continue;
                        if (action == selfFirst) continue;

                        if (wrapped.Contains(action))
                        {
                            rebuilt += action;
                            continue;
                        }

                        Action wrappedAction = WrapWithTimer(action);
                        wrapped.Add(wrappedAction);
                        rebuilt += wrappedAction;
                    }

                    quittingField.SetValue(null, rebuilt);
                }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.UNSPECIFIED); }
            }

            private static Action WrapWithTimer(Action original)
            {
                string label = $"{original.Method.DeclaringType?.FullName}.{original.Method.Name}";

                return () =>
                {
                    Stopwatch sw = Stopwatch.StartNew();

                    try { original(); }
                    catch (Exception e) { ReportHub.LogException(e, ReportCategory.UNSPECIFIED); }
                    finally { ReportHub.LogProductionInfo($"[ExitUtils.Patch] '{label}' Application.quitting subscriber took {sw.ElapsedMilliseconds}ms"); }
                };
            }
        }
    }
}
