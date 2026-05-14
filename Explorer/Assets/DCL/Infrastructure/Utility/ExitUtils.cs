using System;
using System.Diagnostics;
using DCL.Diagnostics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Utility
{
    public static class ExitUtils
    {
        public static event Action BeforeApplicationQuitting;

        public static void Exit()
        {
            var stopwatch = Stopwatch.StartNew();
            ReportHub.LogProductionInfo($"[ExitUtils] Exit requested at {stopwatch.ElapsedMilliseconds}ms");

            BeforeApplicationQuitting?.Invoke();
            ReportHub.LogProductionInfo($"[ExitUtils] BeforeApplicationQuitting handlers finished at {stopwatch.ElapsedMilliseconds}ms");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
            ReportHub.LogProductionInfo($"[ExitUtils] Quit call dispatched at {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
