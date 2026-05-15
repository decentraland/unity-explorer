#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using DCL.Diagnostics;
using UnityEngine;
using Utility.Multithreading;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SubscribeToApplicationQuitting()
        {
#if UNITY_EDITOR
            Patch.Reset();
#endif
            // Dirty reflection hack because there is no other way to view the subs of Application.quitting
            Patch.ApplicationQuittingFirstSubscriberSelfPatchWithTimers();

#if UNITY_EDITOR
            // ensure isExiting is false on reopening
            isExiting.Set(false);
            using (var scope = candidates.Lock()) // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
                scope.Value.Clear();
#endif
            Application.quitting += OnApplicationQuitting;
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

        public static void Exit()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ReportHub.LogProductionInfo($"[ExitUtils] Exit requested at {DateTime.UtcNow:O}");

            if (isExiting)
            {
                ReportHub.LogProductionInfo("[ExitUtils] Exit() ignored because it is already in progress");
                return;
            }

            isExiting.Set(true);

            using (var scope = candidates.Lock()) // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            {
                foreach (OnQuittingCleanUpCandidate candidate in scope.Value)
                    candidate.Execute(stopwatch);
            }

            ReportHub.LogProductionInfo($"[ExitUtils] CleanUpCandidates finished at {stopwatch.ElapsedMilliseconds}ms");

            // Reflection may drop the values. resubscribe to be sure. 
            Patch.ApplicationQuittingFirstSubscriberSelfPatchWithTimers();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
            ReportHub.LogProductionInfo($"[ExitUtils] Quit call dispatched at {stopwatch.ElapsedMilliseconds}ms");
        }

        private static class Patch
        {
            private static readonly HashSet<Action> wrapped = new ();
            private static FieldInfo quittingField;

            // Method is safe to be called multiple times. Idempotency
            public static void ApplicationQuittingFirstSubscriberSelfPatchWithTimers()
            {
                ReportHub.LogProductionInfo($"[ExitUtils.Patch] Invoke ApplicationQuittingFirstSubscriberSelfPatchWithTimers, actions wrapped {wrapped.Count}"); 

                try
                {
                    quittingField ??= typeof(Application).GetField("quitting", BindingFlags.NonPublic | BindingFlags.Static);

                    if (quittingField == null)
                    {
                        ReportHub.LogProductionInfo("[ExitUtils.Patch] Cannot find Application.quitting backing field, per-subscriber timing disabled");
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

            public static void Reset()
            {
                wrapped.Clear();
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
