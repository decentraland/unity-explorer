using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Prefs;
using ECS.Abstract;
using MVC;
using System;
using UnityEngine;

namespace DCL.BugReporting.UI
{
    /// <summary>
    ///     Watches frame times and offers the bug report form when performance degrades. The offer
    ///     is made at most once per session, and never again once the user opts out on the prompt.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PerformanceIssuePromptSystem : BaseUnityLoopSystem
    {
        private const float DEBUG_HICCUP_SECONDS = 2.5f;

        private readonly IMVCManager mvcManager;
        private readonly PerformanceIssueDetector detector;
        private readonly Func<bool> isLoadingScreenOn;

        private bool promptExhausted;
        private bool wasPaused = true;
        private bool debugHiccupPending;

        internal PerformanceIssuePromptSystem(World world, IMVCManager mvcManager, PerformanceIssueDetector detector, Func<bool> isLoadingScreenOn, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.mvcManager = mvcManager;
            this.detector = detector;
            this.isLoadingScreenOn = isLoadingScreenOn;
            promptExhausted = DCLPlayerPrefs.GetBool(DCLPrefKeys.BUG_REPORT_PERFORMANCE_PROMPT_DISMISSED);

            // Debug trigger: the next unpaused frame is fed to the detector as a synthetic hiccup,
            // so the whole production pipeline runs, detection, pause guards, the one-per-session
            // offer and the opt-out included.
            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.BUG_REPORT)
                       ?.AddSingleButton("Simulate Performance Hiccup", () => debugHiccupPending = true);
        }

        protected override void Update(float t)
        {
            if (promptExhausted)
                return;

            // A modal view, the loading screen or an unfocused window means either a flow the
            // prompt must not interrupt or a state whose frame times are not gameplay evidence.
            // The loading screen needs its own check: it lives on the Overlay layer, which does
            // not count as modal.
            if (mvcManager.IsAnyModalViewShowing() || isLoadingScreenOn() || !Application.isFocused)
            {
                detector.Reset();
                wasPaused = true;
                return;
            }

            // The first frame after a pause carries the delta of whatever ended it (a refocus
            // stall, the post-loading scene activation spike), which would read as a fake freeze.
            if (wasPaused)
            {
                wasPaused = false;
                return;
            }

            float frameSeconds = t;

            if (debugHiccupPending)
            {
                debugHiccupPending = false;
                frameSeconds = DEBUG_HICCUP_SECONDS;
            }

            if (!detector.OnFrame(frameSeconds, out PerformanceIssue issue))
                return;

            // One offer per session: a prompt the user ignored must not reappear minutes later.
            promptExhausted = true;
            ShowPromptAsync(issue).Forget();
        }

        private async UniTaskVoid ShowPromptAsync(PerformanceIssue issue)
        {
            try
            {
                await mvcManager.ShowAsync(PerformanceIssuePromptController.IssueCommand(new PerformanceIssuePromptParams(DescribeIssue(issue))));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UNSPECIFIED); }
        }

        private static string DescribeIssue(PerformanceIssue issue) =>
            issue.IsHiccup
                ? $"[Auto-detected] The client froze for {issue.Value:F1} seconds.\n\n"
                : $"[Auto-detected] The frame rate averaged {issue.Value:F0} FPS.\n\n";
    }
}
