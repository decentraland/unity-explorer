using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.SystemMenu
{
    public class SystemMenuController : ControllerBase<SystemMenuView>
    {
        public event Action OnClosed;

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

        public SystemMenuController(
            ViewFactoryMethod viewFactory,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager
        ) : base(viewFactory)
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

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) => UniTask.Never(ct);

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.LogoutButton.onClick.AddListener(Logout);
            viewInstance.ExitAppButton.onClick.AddListener(ExitApp);
            viewInstance.PrivacyPolicyButton.onClick.AddListener(ShowPrivacyPolicy);
            viewInstance.TermsOfServiceButton.onClick.AddListener(ShowTermsOfService);
            viewInstance.PreviewProfileButton.onClick.AddListener(ShowPassport);

            viewInstance!.LogoutButton.onClick.AddListener(CloseView);
            viewInstance.ExitAppButton.onClick.AddListener(CloseView);
            viewInstance.PrivacyPolicyButton.onClick.AddListener(CloseView);
            viewInstance.TermsOfServiceButton.onClick.AddListener(CloseView);
            viewInstance.PreviewProfileButton.onClick.AddListener(CloseView);
        }

        private void CloseView()
        {
            OnClosed?.Invoke();
        }

        private void ShowTermsOfService() =>
            webBrowser.OpenUrl(DecentralandUrl.TermsOfUse);

        private void ShowPrivacyPolicy() =>
            webBrowser.OpenUrl(DecentralandUrl.PrivacyPolicy);

        private void ShowPassport()
        {
            string userId = web3IdentityCache.Identity?.Address ?? string.Empty;

            if (string.IsNullOrEmpty(userId))
                return;

            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId, isOwnProfile: true))).Forget();
        }

        private void ExitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            return;
#endif
            Application.Quit();
        }

        private void Logout()
        {
            async UniTaskVoid LogoutAsync(CancellationToken ct)
            {
                Web3Address address = web3IdentityCache.Identity!.Address;

                await web3Authenticator.LogoutAsync(ct);

                profileCache.Remove(address);

                await userInAppInitializationFlow.ExecuteAsync(
                    new UserInAppInitializationFlowParameters
                    {
                        ShowAuthentication = true,
                        ShowLoading = true,
                        // We have to reload the realm so the scenes are recreated when coming back to the world
                        // The realm fetches the scene entity definitions again and creates the components in ecs
                        // so the SceneFacade can be later attached into the entity
                        ReloadRealm = true,
                        FromLogout = true,
                        World = world,
                        PlayerEntity = playerEntity,
                    }, ct);
            }

            logoutCts = logoutCts.SafeRestart();
            LogoutAsync(logoutCts.Token).Forget();
        }
    }
}
