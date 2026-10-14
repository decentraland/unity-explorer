using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.BugReporting.UI;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.SystemMenu;
using DCL.Utilities.Extensions;
using DCL.Utility;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fills the profile dropdown of the lobby with the own profile and runs the actions of its items.
    /// </summary>
    public class LobbyProfileMenuPresenter : IDisposable
    {
        private const string GUEST_NAME = "Guest";
        private const int COPIED_FEEDBACK_MS = 1000;

        private static readonly Color GUEST_NAME_COLOR = Color.white;

        /// <summary>The Settings item was picked.</summary>
        public Action? SettingsRequested;

        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ISystemClipboard clipboard;
        private readonly IPassportBridge passportBridge;
        private readonly IMVCManager mvcManager;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly UserLogout logout;
        private readonly LobbyProfilePictureBinding picture;
        private readonly bool canReportBug;

        private LobbyProfileMenuElement? menu;
        private string address = string.Empty;
        private CancellationToken showCt;
        private CancellationTokenSource? profileCts;
        private CancellationTokenSource? copiedCts;

        // Independent of the show token: the lobby hides during the logout and must not cancel it.
        private CancellationTokenSource? logoutCts;

        public bool IsOpen => menu is { IsOpen: true };

        public LobbyProfileMenuPresenter(IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ISystemClipboard clipboard,
            IPassportBridge passportBridge,
            IMVCManager mvcManager,
            UnityAppWebBrowser webBrowser,
            UserLogout logout)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.clipboard = clipboard;
            this.passportBridge = passportBridge;
            this.mvcManager = mvcManager;
            this.webBrowser = webBrowser;
            this.logout = logout;
            picture = new LobbyProfilePictureBinding(OnPictureUpdated);
            canReportBug = FeaturesRegistry.Instance.IsEnabled(FeatureId.BugReport);
        }

        public void Dispose()
        {
            Hide();
            picture.Dispose();
            profileCts.SafeCancelAndDispose();
            copiedCts.SafeCancelAndDispose();
            logoutCts.SafeCancelAndDispose();
        }

        // The handlers are assigned rather than added, so a show after a hide binds the same element again cleanly.
        public void Show(LobbyProfileMenuElement menuElement, CancellationToken ct)
        {
            menu = menuElement;
            showCt = ct;

            menuElement.CanReportBug = canReportBug;
            menuElement.OutsideClicked = Dismiss;
            menuElement.CopyAddressClicked = CopyAddress;
            menuElement.ProfileClicked = ShowProfile;
            menuElement.SettingsClicked = ShowSettings;
            menuElement.ReportBugClicked = ReportBug;
            menuElement.SignOutClicked = SignOut;
            menuElement.ExitClicked = Exit;
            menuElement.TermsClicked = ShowTermsOfService;
            menuElement.PrivacyClicked = ShowPrivacyPolicy;
            menuElement.IsCopiedShown = false;
            menuElement.Close();
        }

        public void Hide()
        {
            Dismiss();

            if (menu == null) return;

            menu.OutsideClicked = null;
            menu.CopyAddressClicked = null;
            menu.ProfileClicked = null;
            menu.SettingsClicked = null;
            menu.ReportBugClicked = null;
            menu.SignOutClicked = null;
            menu.ExitClicked = null;
            menu.TermsClicked = null;
            menu.PrivacyClicked = null;
            menu = null;
        }

        public void Toggle()
        {
            if (IsOpen)
                Dismiss();
            else
                Open();
        }

        public void Open()
        {
            if (menu == null) return;

            menu.Open();
            profileCts = profileCts.SafeRestartLinked(showCt);
            LoadProfileAsync(menu, profileCts.Token).Forget();
        }

        public void Dismiss()
        {
            if (menu is not { IsOpen: true }) return;

            profileCts.SafeCancelAndDispose();
            profileCts = null;
            copiedCts.SafeCancelAndDispose();
            copiedCts = null;
            menu.IsCopiedShown = false;
            menu.Close();
        }

        // The address comes from the identity, so it shows even when the profile cannot be fetched.
        private async UniTaskVoid LoadProfileAsync(LobbyProfileMenuElement menuElement, CancellationToken ct)
        {
            if (identityCache.Identity == null)
            {
                address = string.Empty;
                menuElement.Address = string.Empty;
                ShowIdentity(menuElement, GUEST_NAME, GUEST_NAME_COLOR, isVerified: false);
                picture.Clear();
                return;
            }

            address = identityCache.Identity.Address;
            menuElement.Address = Web3Address.Shorten(address);

            Result<Profile.CompactInfo?> result = await profileRepository.GetCompactAsync(address, ct).SuppressToResultAsync(ReportCategory.PROFILE);

            if (ct.IsCancellationRequested) return;

            if (!result.Success || result.Value == null)
            {
                ShowIdentity(menuElement, GUEST_NAME, GUEST_NAME_COLOR, isVerified: false);
                picture.Clear();
                return;
            }

            Profile.CompactInfo profile = result.Value.Value;
            ShowIdentity(menuElement, string.IsNullOrEmpty(profile.ValidatedName) ? GUEST_NAME : profile.ValidatedName, profile.UserNameColor, profile.HasClaimedName);
            picture.Load(in profile, ct);
        }

        private static void ShowIdentity(LobbyProfileMenuElement menuElement, string userName, Color userNameColor, bool isVerified)
        {
            menuElement.UserName = userName;
            menuElement.UserNameColor = userNameColor;
            menuElement.IsVerified = isVerified;
        }

        private void OnPictureUpdated(ProfileThumbnailViewModel model) =>
            menu?.SetPicture(model.Sprite, model.ProfileColor, model.ThumbnailState == ProfileThumbnailViewModel.State.Loading && model.Sprite == null);

        private void CopyAddress()
        {
            if (menu == null || address.Length == 0) return;

            clipboard.Set(address);
            copiedCts = copiedCts.SafeRestartLinked(showCt);
            ShowCopiedAsync(menu, copiedCts.Token).Forget();
        }

        private static async UniTaskVoid ShowCopiedAsync(LobbyProfileMenuElement menuElement, CancellationToken ct)
        {
            menuElement.IsCopiedShown = true;

            bool cancelled = await UniTask.Delay(COPIED_FEEDBACK_MS, cancellationToken: ct).SuppressCancellationThrow();

            // A dismiss already cleared it.
            if (cancelled) return;

            menuElement.IsCopiedShown = false;
        }

        private void ShowProfile()
        {
            Dismiss();

            if (address.Length == 0) return;

            passportBridge.ShowAsync(new PassportParams(address, isOwnProfile: true)).SuppressToResultAsync(ReportCategory.PROFILE).Forget();
        }

        private void ShowSettings()
        {
            Dismiss();
            SettingsRequested?.Invoke();
        }

        private void ReportBug()
        {
            Dismiss();
            mvcManager.ShowAndForget(BugReportController.IssueCommand(new BugReportParams()));
        }

        private void SignOut()
        {
            Dismiss();
            logoutCts = logoutCts.SafeRestart();
            logout.LogoutAsync(logoutCts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
        }

        private void Exit()
        {
            Dismiss();
            ExitUtils.Exit();
        }

        private void ShowTermsOfService()
        {
            Dismiss();
            webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.TermsOfUse);
        }

        private void ShowPrivacyPolicy()
        {
            Dismiss();
            webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.PrivacyPolicy);
        }
    }
}
