using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsScreenController : ControllerBase<MinimumSpecsScreenView>
    {
        public const string PLAYER_PREF_DONT_SHOW_MINIMUM_SPECS_KEY = "dontShowMinSpecsScreen";

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
            viewInstance.ExitButton.onClick.AddListener(OnExitClicked);
            viewInstance.ContinueButton.onClick.AddListener(OnContinueClicked);
            viewInstance.ReadMoreButton.onClick.AddListener(OnReadMoreClicked);
            viewInstance.DontShowAgainToggle.onValueChanged.AddListener(OnToggleChanged);
        }

        private void OnToggleChanged(bool dontShowAgain)
        {
            PlayerPrefs.SetInt(PLAYER_PREF_DONT_SHOW_MINIMUM_SPECS_KEY, dontShowAgain ? 1 : 0);
            PlayerPrefs.Save();
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
            HoldingTask?.TrySetResult();
        }

        private static void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private void OnReadMoreClicked() =>
            webBrowser.OpenUrl(DecentralandUrl.MinimumSpecs);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            HoldingTask.Task;
    }
}
