using Cysharp.Threading.Tasks;
using DCL.ApplicationGuards;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using MVC;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsScreenController : ControllerBase<MinimumSpecsScreenView>
    {
        private readonly IWebBrowser webBrowser;
        private readonly IAnalyticsController analytics;
        private readonly IReadOnlyList<SpecResult> specResult;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;
        public readonly UniTaskCompletionSource HoldingTask;

        private MinimumSpecsTablePresenter specsTablePresenter;

        public MinimumSpecsScreenController(ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IAnalyticsController analytics,
            IReadOnlyList<SpecResult> specResult) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.analytics = analytics;
            this.specResult = specResult;
            HoldingTask = new UniTaskCompletionSource();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.ExitButton.onClick.AddListener(OnExitClicked);
            viewInstance.ContinueButton.onClick.AddListener(OnContinueClicked);
            viewInstance.ReadMoreButton.onClick.AddListener(OnReadMoreClicked);
            viewInstance.DontShowAgainToggle.SetIsOnWithoutNotify(DCLPlayerPrefs.GetBool(DCLPrefKeys.DONT_SHOW_MIN_SPECS_SCREEN));
            viewInstance.DontShowAgainToggle.onValueChanged.AddListener(OnToggleChanged);

            specsTablePresenter = new MinimumSpecsTablePresenter(viewInstance.TableView);
            specsTablePresenter.Populate(specResult);
        }

        private void OnToggleChanged(bool dontShowAgain)
        {
            ReportHub.Log(ReportData.UNSPECIFIED, $"OnToggleChanged [before]: {dontShowAgain}");
            DCLPlayerPrefs.SetBool(DCLPrefKeys.DONT_SHOW_MIN_SPECS_SCREEN, dontShowAgain, true);
            ReportHub.Log(ReportData.UNSPECIFIED, $"OnToggleChanged [after]: {DCLPlayerPrefs.GetBool(DCLPrefKeys.DONT_SHOW_MIN_SPECS_SCREEN)}");

        }

        public override void Dispose()
        {
            if (viewInstance == null)
                return;

            viewInstance.ExitButton.onClick.RemoveListener(OnExitClicked);
            viewInstance.ContinueButton.onClick.RemoveListener(OnContinueClicked);
            viewInstance.ReadMoreButton.onClick.RemoveListener(OnReadMoreClicked);
            viewInstance.DontShowAgainToggle.onValueChanged.RemoveListener(OnToggleChanged);
            HoldingTask?.TrySetResult();
        }

        private void OnContinueClicked()
        {
            analytics.Track(AnalyticsEvents.UI.SKIP_MINIMUM_REQUIREMENTS_SCREEN);
            HoldingTask?.TrySetResult();
        }

        private void OnExitClicked()
        {
            analytics.Track(AnalyticsEvents.UI.EXIT_APP_FROM_MINIMUM_REQUIREMENTS_SCREEN, null, true);
            ExitUtils.Exit();
        }

        private void OnReadMoreClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.MinimumSpecs);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return HoldingTask.Task;
        }
    }
}
