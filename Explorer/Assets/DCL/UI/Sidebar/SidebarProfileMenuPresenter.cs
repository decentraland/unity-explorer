using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.SystemMenu;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.Sidebar
{
    /// <summary>
    ///     Fills and drives the profile menu nested under the sidebar profile button.
    /// </summary>
    public class SidebarProfileMenuPresenter : IDisposable
    {
        private const string GUEST_NAME = "Guest";
        private const string HASH_COLOR = "#727272";
        private const int TOAST_DURATION_MS = 3000;
        private const int TOAST_FADE_MS = 300;
        private const int ADDRESS_PREFIX_LENGTH = 6;
        private const int ADDRESS_SUFFIX_LENGTH = 4;
        private const string INVITE_PATH = "invite";

        /// <summary>
        ///     The passport opens its badges section for any non-empty badge id, and falls back to the first badge
        ///     when the id matches none.
        /// </summary>
        private const string BADGES_SECTION_SENTINEL = "badges";

        private readonly SidebarView view;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ISystemClipboard clipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IPassportBridge passportBridge;
        private readonly IMVCManager mvcManager;
        private readonly UserLogout userLogout;
        private readonly URLBuilder urlBuilder = new ();
        private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());

        private CancellationTokenSource? loadProfileCts;
        private CancellationTokenSource? copyToastCts;
        private CancellationTokenSource? inviteToastCts;
        private CancellationTokenSource? logoutCts;

        public SidebarProfileMenuPresenter(
            SidebarView view,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ProfileChangesBus profileChangesBus,
            ISystemClipboard clipboard,
            IDecentralandUrlsSource decentralandUrlsSource,
            IPassportBridge passportBridge,
            IMVCManager mvcManager,
            UserLogout userLogout)
        {
            this.view = view;
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.profileChangesBus = profileChangesBus;
            this.clipboard = clipboard;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.passportBridge = passportBridge;
            this.mvcManager = mvcManager;
            this.userLogout = userLogout;

            view.ProfilePicture.Bind(thumbnail);

            view.ProfileCopyWalletButton.onClick.AddListener(CopyWalletAddress);
            view.ProfileInviteFriendsButton.onClick.AddListener(CopyInviteLink);
            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);
        }

        public void Dispose()
        {
            view.ProfileCopyWalletButton.onClick.RemoveListener(CopyWalletAddress);
            view.ProfileInviteFriendsButton.onClick.RemoveListener(CopyInviteLink);
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            loadProfileCts.SafeCancelAndDispose();
            copyToastCts.SafeCancelAndDispose();
            inviteToastCts.SafeCancelAndDispose();
            logoutCts.SafeCancelAndDispose();
        }

        public void Refresh()
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            RefreshAsync(loadProfileCts.Token).Forget();
        }

        public void OpenBadges() => ShowOwnPassport(BADGES_SECTION_SENTINEL);

        public void OpenProfile() => ShowOwnPassport(null);

        public void OpenBackpack() =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack)));

        public void Exit() => ExitUtils.Exit();

        public void SignOut()
        {
            logoutCts = logoutCts.SafeRestart();
            SignOutAsync(logoutCts.Token).Forget();
        }

        private void OnProfileUpdated(Profile profile) => Refresh();

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            if (identityCache.Identity == null) return;

            try
            {
                Profile.CompactInfo? compactInfo = await profileRepository.GetCompactAsync(identityCache.Identity.Address, ct);

                if (compactInfo == null) return;

                Profile.CompactInfo profile = compactInfo.Value;

                string wallet = identityCache.Identity.Address;

                view.ProfileNameLabel.text = BuildName(profile);
                view.ProfileVerifiedMark.SetActive(profile.HasClaimedName);
                view.ProfileFoundationMark.SetActive(OfficialWalletsHelper.Instance.IsOfficialWallet(wallet));
                view.ProfileWalletAddressLabel.text = ShortenAddress(wallet);

                thumbnail.SetLoading(profile.UserNameColor);
                await GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, profile, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
        }

        /// <summary>
        ///     Claimed names are shown in the colour the profile system already calculated. Unclaimed names keep that
        ///     colour for the name itself and render the wallet suffix in the muted tag colour.
        /// </summary>
        private static string BuildName(in Profile.CompactInfo profile)
        {
            string name = string.IsNullOrEmpty(profile.ValidatedName) ? GUEST_NAME : profile.ValidatedName;
            string nameColor = ColorUtility.ToHtmlStringRGB(profile.UserNameColor);

            if (profile.HasClaimedName || string.IsNullOrEmpty(profile.WalletId))
                return $"<color=#{nameColor}>{name}</color>";

            return $"<color=#{nameColor}>{name}</color><color={HASH_COLOR}>{profile.WalletId}</color>";
        }

        private static string ShortenAddress(string wallet) =>
            wallet.Length > ADDRESS_PREFIX_LENGTH + ADDRESS_SUFFIX_LENGTH
                ? string.Concat(wallet[..ADDRESS_PREFIX_LENGTH], "...", wallet[^ADDRESS_SUFFIX_LENGTH..])
                : wallet;

        private void CopyWalletAddress()
        {
            if (identityCache.Identity == null) return;

            clipboard.Set(identityCache.Identity.Address);

            copyToastCts = copyToastCts.SafeRestart();
            ShowToastAsync(view.ProfileCopyWalletToast, copyToastCts.Token).Forget();
        }

        private void CopyInviteLink()
        {
            if (identityCache.Identity == null) return;

            urlBuilder.Clear();

            URLAddress url = urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Host)))
                                       .AppendPath(URLPath.FromString($"{INVITE_PATH}/{identityCache.Identity.Address}"))
                                       .Build();

            clipboard.Set(url.Value);

            inviteToastCts = inviteToastCts.SafeRestart();
            ShowToastAsync(view.ProfileInviteToast, inviteToastCts.Token).Forget();
        }

        private static async UniTaskVoid ShowToastAsync(WarningNotificationView toast, CancellationToken ct)
        {
            try
            {
                toast.CanvasGroup.alpha = 0;
                toast.gameObject.SetActive(true);

                await toast.AnimatedShowAsync(TOAST_DURATION_MS, ct);

                // AnimatedShowAsync returns as soon as the fade-out starts, so the object stays up until it finishes.
                await UniTask.Delay(TOAST_FADE_MS, cancellationToken: ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
            finally { toast.gameObject.SetActive(false); }
        }

        private void ShowOwnPassport(string? badgeIdSelected)
        {
            if (identityCache.Identity == null) return;

            string userId = identityCache.Identity.Address;
            passportBridge.ShowAsync(new PassportParams(userId, badgeIdSelected, isOwnProfile: true))
                          .SuppressToResultAsync(ReportCategory.PROFILE)
                          .Forget();
        }

        private async UniTaskVoid SignOutAsync(CancellationToken ct)
        {
            try { await userLogout.LogoutAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }
    }
}
