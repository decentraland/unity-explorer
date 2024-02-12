using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Web3.Authenticators;
using MVC;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.ExplorePanel
{
    public class SystemMenuController : ControllerBase<SystemMenuView>
    {
        private const string PRIVACY_POLICY_URL = "https://decentraland.org/privacy";
        private const string TERMS_URL = "https://decentraland.org/terms";

        private readonly IWebBrowser webBrowser;
        private readonly IWeb3Authenticator web3Authenticator;

        private CancellationTokenSource? logoutCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public SystemMenuController(ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator)
            : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
        }

        public override void Dispose()
        {
            base.Dispose();

            logoutCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance.CloseButton.OnClickAsync(ct);

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.LogoutButton.onClick.AddListener(Logout);
            viewInstance.ExitAppButton.onClick.AddListener(ExitApp);
            viewInstance.PrivacyPolicyButton.onClick.AddListener(ShowPrivacyPolicy);
            viewInstance.TermsOfServiceButton.onClick.AddListener(ShowTermsOfService);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            logoutCts.SafeCancelAndDispose();
        }

        private void ShowTermsOfService() =>
            webBrowser.OpenUrl(TERMS_URL);

        private void ShowPrivacyPolicy() =>
            webBrowser.OpenUrl(PRIVACY_POLICY_URL);

        private void ExitApp() =>
            Application.Quit();

        private void Logout()
        {
            async UniTaskVoid LogoutAsync(CancellationToken ct)
            {
                await web3Authenticator.LogoutAsync(ct);

                // TODO: start authentication flow
            }

            logoutCts = logoutCts.SafeRestart();
            LogoutAsync(logoutCts.Token).Forget();
        }
    }
}
