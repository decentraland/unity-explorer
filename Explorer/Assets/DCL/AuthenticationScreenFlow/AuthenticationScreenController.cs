using Cysharp.Threading.Tasks;
using DCL.Web3Authentication;
using MVC;
using System;
using System.Threading;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        private readonly IWeb3Authenticator web3Authenticator;

        private CancellationTokenSource? loginCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public AuthenticationScreenController(ViewFactoryMethod viewFactory,
            IWeb3Authenticator web3Authenticator)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
        }

        public override void Dispose()
        {
            base.Dispose();

            CancelLoginProcess();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.LoginButton.onClick.AddListener(Login);
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            viewInstance.PendingAuthentication.SetActive(false);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            CancelLoginProcess();
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            (lifeCycleTask ??= new UniTaskCompletionSource()).Task.AttachExternalCancellation(ct);

        private void Login()
        {
            async UniTaskVoid LoginAsync(CancellationToken ct)
            {
                try
                {
                    viewInstance.PendingAuthentication.SetActive(true);
                    await web3Authenticator.LoginAsync(ct);
                    lifeCycleTask!.TrySetResult();
                    lifeCycleTask = null;
                }
                finally { viewInstance.PendingAuthentication.SetActive(false); }
            }

            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            LoginAsync(loginCancellationToken.Token).Forget();
        }

        private void CancelLoginProcess()
        {
            try
            {
                loginCancellationToken?.Cancel();
                loginCancellationToken?.Dispose();
            }
            catch (ObjectDisposedException) { }

            loginCancellationToken = null;
        }
    }
}
