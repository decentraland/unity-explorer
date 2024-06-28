using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Profiles;
using DCL.UI;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleController : IPassportModuleController
    {
        private readonly UserBasicInfo_PassportModuleView view;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;

        private Profile currentProfile;
        private CancellationTokenSource copyNameCts;
        private CancellationTokenSource copyWalletCts;

        public UserBasicInfo_PassportModuleController(UserBasicInfo_PassportModuleView view, ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.view = view;
            this.chatEntryConfiguration = chatEntryConfiguration;
            view.CopyNameWarningNotification.Hide(true);
            view.CopyWalletWarningNotification.Hide(true);

            view.CopyUserNameButton.onClick.AddListener(() =>
            {
                if (currentProfile == null)
                    return;

                CopyToClipboard(currentProfile.HasClaimedName ? view.UserNameText.text : $"{currentProfile.Name}#{currentProfile.UserId[^4..]}");
                copyNameCts = copyNameCts.SafeRestart();
                ShowCopyWarningAsync(view.CopyNameWarningNotification, copyNameCts.Token).Forget();
            });
            view.CopyWalletAddressButton.onClick.AddListener(() =>
            {
                if (currentProfile == null)
                    return;
                
                CopyToClipboard(currentProfile.UserId);
                copyWalletCts = copyWalletCts.SafeRestart();
                ShowCopyWarningAsync(view.CopyWalletWarningNotification, copyWalletCts.Token).Forget();
            });
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            view.UserNameText.text = profile.Name;
            view.UserNameText.color = chatEntryConfiguration.GetNameColor(profile.Name);
            view.UserNameHashtagText.text = $"#{profile.UserId[^4..]}";
            view.UserNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);
            view.VerifiedMark.SetActive(profile.HasClaimedName);
            view.UserWalletAddressText.text = $"{profile.UserId[..3]}...{profile.UserId[^3..]}";

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.UserNameContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.WalletAddressContainer);
        }

        public void Clear() { }

        public void Dispose()
        {
            view.CopyUserNameButton.onClick.RemoveAllListeners();
            view.CopyWalletAddressButton.onClick.RemoveAllListeners();
            view.CopyNameWarningNotification.Hide(true);
            view.CopyWalletWarningNotification.Hide(true);
            Clear();
        }

        private void CopyToClipboard(string text) =>
            GUIUtility.systemCopyBuffer = text;

        private async UniTaskVoid ShowCopyWarningAsync(WarningNotificationView notificationView, CancellationToken ct)
        {
            notificationView.Show();
            await UniTask.Delay(1000, cancellationToken: ct);
            notificationView.Hide();
        }
    }
}
