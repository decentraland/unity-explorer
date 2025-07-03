using Cysharp.Threading.Tasks;
using DCL.ApplicationGuards;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using MVC;
using System.Threading;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsScreenController : ControllerBase<MinimumSpecsScreenView>
    {
        private readonly IWebBrowser webBrowser;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;
        public readonly UniTaskCompletionSource HoldingTask;

        public MinimumSpecsScreenController(ViewFactoryMethod viewFactory, IWebBrowser webBrowser) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            HoldingTask = new UniTaskCompletionSource();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.ExitButton.onClick.AddListener(GuardUtils.Exit);
            viewInstance.ContinueButton.onClick.AddListener(OnContinueClicked);
            viewInstance.ReadMoreButton.onClick.AddListener(OnReadMoreClicked);
            viewInstance.DontShowAgainToggle.onValueChanged.AddListener(OnToggleChanged);
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

            viewInstance.ExitButton.onClick.RemoveListener(GuardUtils.Exit);
            viewInstance.ContinueButton.onClick.RemoveListener(OnContinueClicked);
            viewInstance.ReadMoreButton.onClick.RemoveListener(OnReadMoreClicked);
            viewInstance.DontShowAgainToggle.onValueChanged.RemoveListener(OnToggleChanged);
            HoldingTask?.TrySetResult();
        }

        private void OnContinueClicked()
        {
            HoldingTask?.TrySetResult();
        }



        private void OnReadMoreClicked() =>
            webBrowser.OpenUrl(DecentralandUrl.MinimumSpecs);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            HoldingTask.Task;
    }
}
