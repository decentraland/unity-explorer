using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.UserInAppInitializationFlow;
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
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly Entity playerEntity;
        private readonly World world;

        private CancellationTokenSource? logoutCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public SystemMenuController(ViewFactoryMethod viewFactory,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow)
            : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.playerEntity = playerEntity;
            this.world = world;
        }

        public override void Dispose()
        {
            base.Dispose();

            logoutCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance.LogoutButton.OnClickAsync(ct),
                viewInstance.ExitAppButton.OnClickAsync(ct),
                viewInstance.PrivacyPolicyButton.OnClickAsync(ct),
                viewInstance.TermsOfServiceButton.OnClickAsync(ct),
                viewInstance.CloseButton.OnClickAsync(ct));

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

            // TODO: abstraction (?)
            Application.Quit();

        private void Logout()
        {
            async UniTaskVoid LogoutAsync(CancellationToken ct)
            {
                await web3Authenticator.LogoutAsync(ct);
                await userInAppInitializationFlow.ExecuteAsync(true, true, world, playerEntity, ct);
            }

            logoutCts = logoutCts.SafeRestart();
            LogoutAsync(logoutCts.Token).Forget();
        }
    }
}
