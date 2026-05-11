using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI.Controls;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.UI.Sidebar.HelpMenu
{
    public class HelpMenuController : ControllerBase<HelpMenuView>
    {
        private readonly IMVCManager mvcManager;
        private readonly IWebBrowser webBrowser;

        private UniTaskCompletionSource? closeViewTask;
        private CancellationTokenSource openControlsCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public event Action? ContactSupportClicked;

        public HelpMenuController(ViewFactoryMethod viewFactory, IMVCManager mvcManager, IWebBrowser webBrowser)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.webBrowser = webBrowser;
        }

        public override void Dispose()
        {
            base.Dispose();
            openControlsCts.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.CloseAreaButton.onClick.AddListener(OnClose);
            viewInstance.MouseAndKeyControlsButton.onClick.AddListener(OnMouseAndKeyControlsClicked);
            viewInstance.FaqButton.onClick.AddListener(OnFaqClicked);
            viewInstance.ContactSupportButton.onClick.AddListener(OnContactSupportClicked);
            viewInstance.DiscordButton.onClick.AddListener(OnDiscordClicked);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask = new UniTaskCompletionSource();
            await closeViewTask.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        protected override void OnViewClose() =>
            closeViewTask?.TrySetResult();

        private void OnClose() =>
            closeViewTask?.TrySetResult();

        private void OnMouseAndKeyControlsClicked()
        {
            closeViewTask?.TrySetResult();
            openControlsCts = openControlsCts.SafeRestart();
            mvcManager.ShowAsync(ControlsPanelController.IssueCommand(), openControlsCts.Token).Forget();
        }

        private void OnFaqClicked()
        {
            closeViewTask?.TrySetResult();
            webBrowser.OpenUrl(DecentralandUrl.Faqs);
        }

        private void OnContactSupportClicked()
        {
            closeViewTask?.TrySetResult();
            webBrowser.OpenUrl(DecentralandUrl.Help);
            ContactSupportClicked?.Invoke();
        }

        private void OnDiscordClicked()
        {
            closeViewTask?.TrySetResult();
            webBrowser.OpenUrl(DecentralandUrl.Discord);
        }
    }
}
