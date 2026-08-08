using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Prefs;
using MVC;
using System;
using System.Threading;

namespace DCL.BugReporting.UI
{
    /// <summary>
    ///     Small popup offered when a performance drop is detected: it hands over to the bug report
    ///     form and remembers a permanent opt-out.
    /// </summary>
    public class PerformanceIssuePromptController : ControllerBase<PerformanceIssuePromptView, PerformanceIssuePromptParams>
    {
        private readonly IMVCManager mvcManager;

        private UniTaskCompletionSource? closeIntent;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PerformanceIssuePromptController(ViewFactoryMethod viewFactory, IMVCManager mvcManager) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.CloseButton.onClick.AddListener(Dismiss);
            viewInstance.ReportBugButton.onClick.AddListener(ReportBug);
        }

        protected override void OnBeforeViewShow() =>
            viewInstance!.DontShowAgainToggle.SetIsOnWithoutNotify(false);

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent = new UniTaskCompletionSource();
            await closeIntent.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        private void Dismiss()
        {
            PersistOptOut();
            closeIntent?.TrySetResult();
        }

        private void ReportBug()
        {
            PersistOptOut();
            closeIntent?.TrySetResult();
            OpenBugReportFormAsync().Forget();
        }

        private void PersistOptOut()
        {
            if (viewInstance!.DontShowAgainToggle.isOn)
                DCLPlayerPrefs.SetBool(DCLPrefKeys.BUG_REPORT_PERFORMANCE_PROMPT_DISMISSED, true, save: true);
        }

        private async UniTaskVoid OpenBugReportFormAsync()
        {
            try
            {
                await mvcManager.ShowAsync(BugReportController.IssueCommand(new BugReportParams(inputData.PrefilledDescription, BugReportIssueTypes.PERFORMANCE)));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UNSPECIFIED); }
        }
    }

    /// <summary>Input for showing the performance issue prompt.</summary>
    public readonly struct PerformanceIssuePromptParams
    {
        /// <summary>Auto-detected diagnostic line the bug report form opens with.</summary>
        public readonly string PrefilledDescription;

        public PerformanceIssuePromptParams(string prefilledDescription)
        {
            PrefilledDescription = prefilledDescription;
        }
    }
}
