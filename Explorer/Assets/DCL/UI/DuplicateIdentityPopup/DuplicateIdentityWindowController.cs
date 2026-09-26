using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using DCL.Utility;
using DCL.Diagnostics;
using System;
using MVC;
using System.Threading;

namespace DCL.UI.DuplicateIdentityPopup
{
    public class DuplicateIdentityWindowController : ControllerBase<DuplicateIdentityWindowView>
    {
        private readonly SessionControl? session;
        private readonly Func<CancellationToken, UniTask>? reconnect;
        private bool reconnecting;
        private bool legacyNotice;

        public DuplicateIdentityWindowController(
            ViewFactoryMethod viewFactory, SessionControl? session = null, Func<CancellationToken, UniTask>? reconnect = null) : base(viewFactory)
        {
            this.session = session;
            this.reconnect = reconnect;
        }

        protected override void OnBeforeViewShow()
        {
            legacyNotice = session == null || session.Current == SessionControl.Status.Active;
            DCLInput.Instance.Disable();
            viewInstance!.ExitButton.onClick.RemoveListener(OnExitButtonClicked);
            viewInstance.ExitButton.onClick.AddListener(OnExitButtonClicked);
            Refresh();
        }

        private void OnExitButtonClicked()
        {
            if (session == null || session.Current == SessionControl.Status.Banned || session.Current == SessionControl.Status.Active) ExitUtils.Exit();
            else if (!reconnecting && reconnect != null) ReconnectAsync().Forget();
        }

        private async UniTaskVoid ReconnectAsync()
        {
            reconnecting = true;
            try { if (reconnect != null) await reconnect(CancellationToken.None); }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.LIVEKIT);
            }
            finally { reconnecting = false; }
        }

        private void Refresh()
        {
            if (viewInstance == null) return;
            if (session == null || legacyNotice)
            {
                viewInstance.Title.text = "Session Ended";
                viewInstance.Description.text = "Your Session was ended because your account logged from another location.";
                viewInstance.ActionLabel.text = "EXIT APPLICATION";
                viewInstance.ExitButton.interactable = true;
                return;
            }
            SessionControl.Status status = session.Current;
            viewInstance.Title.text = status switch
            {
                SessionControl.Status.Pending => "Connecting here",
                SessionControl.Status.Superseded => "Connected elsewhere",
                SessionControl.Status.Banned => "Access denied",
                _ => "Connection could not be completed",
            };
            viewInstance.Description.text = status switch
            {
                SessionControl.Status.Pending => "Waiting for your previous session to disconnect…",
                SessionControl.Status.Superseded => "Another session is using this account. Reconnect here to sign in again.",
                SessionControl.Status.Banned => "Access to this service was denied.",
                _ => "Automatic reconnection has stopped. You can sign in again to retry.",
            };
            viewInstance.ActionLabel.text = status == SessionControl.Status.Banned ? "EXIT APPLICATION" : "Reconnect here";
            viewInstance.ExitButton.interactable = status is not (SessionControl.Status.Pending or SessionControl.Status.Authenticating) && !reconnecting;
        }

        protected override void OnViewClose()
        {
            viewInstance!.ExitButton.onClick.RemoveListener(OnExitButtonClicked);
            DCLInput.Instance.Enable();
            base.OnViewClose();
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            do
            {
                if (session != null)
                {
                    if (session.Current != SessionControl.Status.Active) legacyNotice = false;
                    session.CheckWatchdog(DateTime.UtcNow);
                }
                Refresh();
                await UniTask.Delay(250, cancellationToken: ct);
            }
            while (session == null || legacyNotice || session.Current is not (SessionControl.Status.Active or SessionControl.Status.Authenticating));
        }
    }
}


