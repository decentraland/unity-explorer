using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using MVC;
using System.Collections.Generic;
using System.Threading;
using Sentry;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsScreenController : ControllerBase<MinimumSpecsScreenView>
    {
        private readonly IWebBrowser webBrowser;
        private readonly IReadOnlyList<SpecResult> specResult;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;
        public readonly UniTaskCompletionSource HoldingTask;

        private MinimumSpecsTablePresenter specsTablePresenter;

        public MinimumSpecsScreenController(ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IReadOnlyList<SpecResult> specResult) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.specResult = specResult;
            HoldingTask = new UniTaskCompletionSource();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.ExitButton.onClick.AddListener(OnExitClicked);
            viewInstance.ContinueButton.onClick.AddListener(OnContinueClicked);
            viewInstance.ReadMoreButton.onClick.AddListener(OnReadMoreClicked);
            viewInstance.DontShowAgainToggle.onValueChanged.AddListener(OnToggleChanged);

            specsTablePresenter = new MinimumSpecsTablePresenter(viewInstance.TableView);
            specsTablePresenter.Populate(specResult);
        }

        private void OnToggleChanged(bool dontShowAgain)
        {
            DCLPlayerPrefs.SetInt(DCLPrefKeys.DONT_SHOW_MIN_SPECS_SCREEN, dontShowAgain ? 1 : 0);
            DCLPlayerPrefs.Save();
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
            SentrySdk.AddBreadcrumb("Skipping minimum requirements warning screen");
            HoldingTask?.TrySetResult();
        }

        private static void OnExitClicked()
        {
            SentrySdk.AddBreadcrumb("Exiting application on minimum requirements warning screen");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
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