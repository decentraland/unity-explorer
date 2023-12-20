using Cysharp.Threading.Tasks;
using DCL.Web3Authentication;
using MVC;
using System.Threading;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        private readonly IWeb3Authenticator web3Authenticator;

        private CancellationTokenSource? loginCancellationToken;

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

            loginCancellationToken?.Cancel();
            loginCancellationToken?.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.LoginButton.onClick.AddListener(Login);
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            UniTask.Never(ct);

        private void Login()
        {
            async UniTaskVoid LoginAsync(CancellationToken ct)
            {
                viewInstance.PendingAuthentication.SetActive(true);
                await web3Authenticator.LoginAsync(ct);
                viewInstance.PendingAuthentication.SetActive(false);
            }

            loginCancellationToken?.Cancel();
            loginCancellationToken?.Dispose();
            loginCancellationToken = new CancellationTokenSource();
            LoginAsync(loginCancellationToken.Token).Forget();
        }
    }
}
