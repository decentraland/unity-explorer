using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using DCL.Utility;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Threading;
using Utility;

namespace DCL.UI.SystemMenu
{
    public class SystemSectionPresenter : IDisposable
    {
        public event Action? OnClosed;

        private readonly SystemMenuView view;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileCache profileCache;
        private readonly Entity playerEntity;
        private readonly World world;
        private readonly IPassportBridge passportBridge;

        private CancellationTokenSource? logoutCts;

        public SystemSectionPresenter(
            SystemMenuView view,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            IWeb3IdentityCache web3IdentityCache,
            IPassportBridge passportBridge)
        {
            this.view = view;
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.profileCache = profileCache;
            this.web3IdentityCache = web3IdentityCache;
            this.passportBridge = passportBridge;
            this.playerEntity = playerEntity;
            this.world = world;

            SubscribeToEvents();
        }

        public void Dispose()
        {
            logoutCts.SafeCancelAndDispose();
        }

        private void SubscribeToEvents()
        {
            view.LogoutButton.onClick.AddListener(Logout);
            view.ExitAppButton.onClick.AddListener(ExitUtils.Exit);
            view.PrivacyPolicyButton.onClick.AddListener(ShowPrivacyPolicy);
            view.TermsOfServiceButton.onClick.AddListener(ShowTermsOfService);
            view.LogoutButton.onClick.AddListener(CloseView);
            view.ExitAppButton.onClick.AddListener(CloseView);
            view.PrivacyPolicyButton.onClick.AddListener(CloseView);
            view.TermsOfServiceButton.onClick.AddListener(CloseView);
            view.PreviewProfileButton.onClick.AddListener(OnPreviewProfileButtonClickedAsync);
        }

        private void OnPreviewProfileButtonClickedAsync()
        {
            CloseView();
            ShowPassport();
            return;

            void ShowPassport()
            {
                string userId = web3IdentityCache.Identity?.Address ?? string.Empty;

                if (string.IsNullOrEmpty(userId))
                    return;

                passportBridge.ShowAsync(new PassportParams(userId, isOwnProfile: true)).Forget();
            }
        }

        private void CloseView()
        {
            OnClosed?.Invoke();
        }

        private void ShowTermsOfService() =>
            webBrowser.OpenUrl(DecentralandUrl.TermsOfUse);

        private void ShowPrivacyPolicy() =>
            webBrowser.OpenUrl(DecentralandUrl.PrivacyPolicy);


        private void Logout()
        {
            logoutCts = logoutCts.SafeRestart();
            LogoutAsync(logoutCts.Token).Forget();
            return;

            async UniTaskVoid LogoutAsync(CancellationToken ct)
            {
                if (web3IdentityCache.Identity == null)
                {
                    ReportHub.LogError(ReportCategory.UI, "Cannot logout. Identity is null.");
                    return;
                }

                Web3Address address = web3IdentityCache.Identity!.Address;

                await web3Authenticator.LogoutAsync(ct);

                profileCache.Remove(address);

                await userInAppInitializationFlow.ExecuteAsync(
                    new UserInAppInitializationFlowParameters(
                        showAuthentication: true,
                        showLoading: true,
                        loadSource: IUserInAppInitializationFlow.LoadSource.Logout,
                        world: world,
                        playerEntity: playerEntity
                    ),
                    ct
                );
            }
        }
    }
}
