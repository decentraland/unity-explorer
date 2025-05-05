using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using MVC;
using System.Threading;

namespace DCL.ApplicationBlocklistGuard
{
    public class BlockedScreenController : ControllerBase<BlockedScreenView>
    {
        private readonly IWebBrowser webBrowser;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public BlockedScreenController(ViewFactoryMethod viewFactory, IWebBrowser webBrowser) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
        }

        protected override void OnViewInstantiated()
        {
            if (viewInstance != null)
            {
                viewInstance.CloseButton.onClick.AddListener(OnExitClicked);
                viewInstance.SupportButton.onClick.AddListener(OnSupportClicked);
            }
        }

        public override void Dispose()
        {
            if (viewInstance == null)
                return;

            viewInstance.CloseButton.onClick.RemoveListener(OnExitClicked);
            viewInstance.SupportButton.onClick.RemoveListener(OnSupportClicked);
        }

        private static void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private void OnSupportClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
