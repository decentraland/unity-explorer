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

        private bool promptExhausted;

        internal PerformanceIssuePromptSystem(World world, IMVCManager mvcManager, PerformanceIssueDetector detector, IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.mvcManager = mvcManager;
            this.detector = detector;
            promptExhausted = DCLPlayerPrefs.GetBool(DCLPrefKeys.BUG_REPORT_PERFORMANCE_PROMPT_DISMISSED);

            // Debug trigger with a synthetic hiccup: it skips the detector and the one-per-session
            // guard, so the prompt can be exercised repeatedly and regardless of the opt-out.
            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.BUG_REPORT)
                       ?.AddSingleButton("Show Performance Prompt", () => ShowPromptAsync(PerformanceIssue.Hiccup(DEBUG_HICCUP_SECONDS)).Forget());
        }

        protected override void Update(float t)
        {
            if (promptExhausted)
                return;

            // A modal view on screen means either a flow the prompt must not interrupt or a state
            // (loading screen, menus) whose frame times are not gameplay evidence.
            if (mvcManager.IsAnyModalViewShowing())
            {
                detector.Reset();
                return;
            }

            if (!detector.OnFrame(t, out PerformanceIssue issue))
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
