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

        public event Action? ContactSupportRequested;

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

            if (viewInstance == null) return;

            viewInstance.MouseAndKeyControlsClicked -= OnMouseAndKeyControlsClicked;
            viewInstance.FaqClicked -= OnFaqClicked;
            viewInstance.ContactSupportClicked -= OnContactSupportClicked;
            viewInstance.DiscordClicked -= OnDiscordClicked;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.MouseAndKeyControlsClicked += OnMouseAndKeyControlsClicked;
            viewInstance.FaqClicked += OnFaqClicked;
            viewInstance.ContactSupportClicked += OnContactSupportClicked;
            viewInstance.DiscordClicked += OnDiscordClicked;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask = new UniTaskCompletionSource();
            await closeViewTask.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        protected override void OnViewClose() =>
            CloseView();

        private void OnMouseAndKeyControlsClicked()
        {
            CloseView();
            openControlsCts = openControlsCts.SafeRestart();
            mvcManager.ShowAsync(ControlsPanelController.IssueCommand(), openControlsCts.Token).Forget();
        }

        private void OnFaqClicked()
        {
            CloseView();
            webBrowser.OpenUrl(DecentralandUrl.Faqs);
        }

        private void OnContactSupportClicked()
        {
            CloseView();
            webBrowser.OpenUrl(DecentralandUrl.Help);
            ContactSupportRequested?.Invoke();
        }

        private void OnDiscordClicked()
        {
            CloseView();
            webBrowser.OpenUrl(DecentralandUrl.Discord);
        }

        private void CloseView() => closeViewTask?.TrySetResult();
    }
}
