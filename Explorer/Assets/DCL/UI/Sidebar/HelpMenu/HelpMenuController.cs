using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI.Controls;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Sidebar.HelpMenu
{
    public class HelpMenuController : ControllerBase<HelpMenuView>
    {
        private readonly IMVCManager mvcManager;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly SupportRequestService supportRequestService;

        private UniTaskCompletionSource? closeViewTask;
        private CancellationTokenSource openControlsCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public HelpMenuController(ViewFactoryMethod viewFactory, IMVCManager mvcManager, UnityAppWebBrowser webBrowser, SupportRequestService supportRequestService)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.webBrowser = webBrowser;
            this.supportRequestService = supportRequestService;
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
            webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.Faqs);
        }

        private void OnContactSupportClicked()
        {
            CloseView();
            supportRequestService.OpenSupport();
        }

        private void OnDiscordClicked()
        {
            CloseView();
            webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.Discord);
        }

        private void CloseView() => closeViewTask?.TrySetResult();
    }
}
