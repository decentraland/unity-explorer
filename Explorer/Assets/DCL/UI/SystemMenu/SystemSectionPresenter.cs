using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Utilities.Extensions;
using DCL.Utility;
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
        private readonly UserLogout logout;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IPassportBridge passportBridge;

        private CancellationTokenSource? logoutCts;

        public SystemSectionPresenter(
            SystemMenuView view,
            UserLogout logout,
            UnityAppWebBrowser webBrowser,
            IWeb3IdentityCache web3IdentityCache,
            IPassportBridge passportBridge)
        {
            this.view = view;
            this.logout = logout;
            this.webBrowser = webBrowser;
            this.web3IdentityCache = web3IdentityCache;
            this.passportBridge = passportBridge;

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
            webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.TermsOfUse);

        private void ShowPrivacyPolicy() =>
            webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.PrivacyPolicy);

        private void Logout()
        {
            logoutCts = logoutCts.SafeRestart();
            logout.LogoutAsync(logoutCts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
        }
    }
}
