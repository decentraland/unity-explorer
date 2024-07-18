using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Passport;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
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
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileCache profileCache;
        private readonly Entity playerEntity;
        private readonly World world;
        private readonly IMVCManager mvcManager;

        private CancellationTokenSource? logoutCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public SystemMenuController(ViewFactoryMethod viewFactory,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager)
            : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.profileCache = profileCache;
            this.web3IdentityCache = web3IdentityCache;
            this.playerEntity = playerEntity;
            this.world = world;
            this.mvcManager = mvcManager;
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
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.PreviewProfileButton.OnClickAsync(ct));

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.LogoutButton.onClick.AddListener(Logout);
            viewInstance.ExitAppButton.onClick.AddListener(ExitApp);
            viewInstance.PrivacyPolicyButton.onClick.AddListener(ShowPrivacyPolicy);
            viewInstance.TermsOfServiceButton.onClick.AddListener(ShowTermsOfService);
            viewInstance.PreviewProfileButton.onClick.AddListener(ShowPassport);
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

        private void ShowPassport()
        {
            var userId = web3IdentityCache.Identity!.Address;
            if (string.IsNullOrEmpty(userId))
                return;

            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId))).Forget();
        }

        private void ExitApp() =>

            // TODO: abstraction (?)
            Application.Quit();

        private void Logout()
        {
            async UniTaskVoid LogoutAsync(CancellationToken ct)
            {
                Web3Address address = web3IdentityCache.Identity!.Address;
                await web3Authenticator.LogoutAsync(ct);
                profileCache.Remove(address);
                await userInAppInitializationFlow.ExecuteAsync(true, true, world, playerEntity, ct);
            }

            logoutCts = logoutCts.SafeRestart();
            LogoutAsync(logoutCts.Token).Forget();
        }
    }
}
