using System;
using System.Collections.Generic;
using System.Diagnostics;
using DCL.Diagnostics;
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

        public void Execute(Stopwatch stopwatch)
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
        private static readonly Mutex<List<OnQuittingCleanUpCandidate>> candidates = new (new ());
        private static readonly Atomic<bool> isExiting = new (false);

        public static void RegisterCleanUpCandidate(OnQuittingCleanUpCandidate candidate)
        {
            if (isExiting)
            {
                ReportHub.LogProductionInfo($"[ExitUtils] Ignored RegisterCleanUpCandidate('{candidate.Name}') because Exit() is already in progress");
                return;
            }

            using var scope = candidates.Lock();

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

            using var scope = candidates.Lock();

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
            ReportHub.LogProductionInfo($"[ExitUtils] Exit requested at {stopwatch.ElapsedMilliseconds}ms");

            if (isExiting)
            {
                ReportHub.LogProductionInfo("[ExitUtils] Exit() ignored because it is already in progress");
                return;
            }

            isExiting.Set(true);

            using (var scope = candidates.Lock())
            {
                foreach (OnQuittingCleanUpCandidate candidate in scope.Value)
                    candidate.Execute(stopwatch);
            }

            ReportHub.LogProductionInfo($"[ExitUtils] CleanUpCandidates finished at {stopwatch.ElapsedMilliseconds}ms");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
            ReportHub.LogProductionInfo($"[ExitUtils] Quit call dispatched at {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
