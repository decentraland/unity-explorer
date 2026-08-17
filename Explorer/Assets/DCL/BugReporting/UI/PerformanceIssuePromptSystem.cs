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
    ///     repeats on every detected issue until the user opts out on the prompt.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PerformanceIssuePromptSystem : BaseUnityLoopSystem
    {
        private const float DEBUG_HICCUP_SECONDS = 2.5f;

        private readonly IMVCManager mvcManager;
        private readonly PerformanceIssueDetector detector;
        private readonly Func<bool> isLoadingScreenOn;

        private bool optedOut;
        private bool promptShowing;
        private bool wasPaused = true;
        private bool debugHiccupPending;

        internal PerformanceIssuePromptSystem(World world, IMVCManager mvcManager, PerformanceIssueDetector detector, Func<bool> isLoadingScreenOn, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.mvcManager = mvcManager;
            this.detector = detector;
            this.isLoadingScreenOn = isLoadingScreenOn;
            optedOut = DCLPlayerPrefs.GetBool(DCLPrefKeys.BUG_REPORT_PERFORMANCE_PROMPT_DISMISSED);

            // Debug trigger: the next unpaused frame is fed to the detector as a synthetic hiccup,
            // so the whole production pipeline runs, detection, pause guards and the opt-out
            // included.
            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.BUG_REPORT)
                       ?.AddSingleButton("Simulate Performance Hiccup", () => debugHiccupPending = true)
                        .AddSingleButton("Clear Prompt Opt-Out", ClearOptOut);
        }

        protected override void Update(float t)
        {
            if (optedOut)
                return;

            // A modal view, the loading screen or an unfocused window means either a flow the
            // prompt must not interrupt or a state whose frame times are not gameplay evidence.
            // The loading screen needs its own check: it lives on the Overlay layer, which does
            // not count as modal. The in-flight prompt needs one too: it covers the frames between
            // requesting the show and the view registering as a modal.
            if (promptShowing || mvcManager.IsAnyModalViewShowing() || isLoadingScreenOn() || !Application.isFocused)
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

            ShowPromptAsync(issue).Forget();
        }

        private void ClearOptOut()
        {
            DCLPlayerPrefs.SetBool(DCLPrefKeys.BUG_REPORT_PERFORMANCE_PROMPT_DISMISSED, false, save: true);
            optedOut = false;
        }

        private async UniTaskVoid ShowPromptAsync(PerformanceIssue issue)
        {
            promptShowing = true;

            try
            {
                await mvcManager.ShowAsync(PerformanceIssuePromptController.IssueCommand(new PerformanceIssuePromptParams(DescribeIssue(issue))));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UNSPECIFIED); }
            finally
            {
                promptShowing = false;

                // The prompt is where the opt-out is persisted, so its closing is the moment the
                // stored preference can have changed.
                optedOut = DCLPlayerPrefs.GetBool(DCLPrefKeys.BUG_REPORT_PERFORMANCE_PROMPT_DISMISSED);
            }
        }

        private static string DescribeIssue(PerformanceIssue issue) =>
            issue.IsHiccup
                ? $"[Auto-detected] The client froze for {issue.Value:F1} seconds.\n\n"
                : $"[Auto-detected] The frame rate averaged {issue.Value:F0} FPS.\n\n";
    }
}
