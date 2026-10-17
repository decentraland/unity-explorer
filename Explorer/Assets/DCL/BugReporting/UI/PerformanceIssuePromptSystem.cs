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

            // Feeds the next unpaused frame to the detector as a synthetic hiccup, exercising the production pipeline.
            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.BUG_REPORT)
                       ?.AddSingleButton("Simulate Performance Hiccup", () => debugHiccupPending = true)
                        .AddSingleButton("Clear Prompt Opt-Out", ClearOptOut);
        }

        protected override void Update(float t)
        {
            if (optedOut)
                return;

            // The loading screen lives on the Overlay layer (not modal) and the in-flight prompt is not yet registered as one, so both need their own check.
            if (promptShowing || mvcManager.IsAnyModalViewShowing() || isLoadingScreenOn() || !Application.isFocused)
            {
                detector.Reset();
                wasPaused = true;
                return;
            }

            // The first frame after a pause carries the delta of whatever ended it, which would read as a fake freeze.
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
                optedOut = DCLPlayerPrefs.GetBool(DCLPrefKeys.BUG_REPORT_PERFORMANCE_PROMPT_DISMISSED);
            }
        }

        private static string DescribeIssue(PerformanceIssue issue) =>
            issue.IsHiccup
                ? $"[Auto-detected] The client froze for {issue.Value:F1} seconds.\n\n"
                : $"[Auto-detected] The frame rate averaged {issue.Value:F0} FPS.\n\n";
    }
}
