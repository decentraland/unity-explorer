using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Profiles;
using DCL.Profiles.Self;
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
        private readonly ISelfProfile selfProfile;

        private Profile currentProfile;
        private CancellationTokenSource checkEditionAvailabilityCts;

        public UserBasicInfo_PassportModuleController(
            UserBasicInfo_PassportModuleView view,
            ChatEntryConfigurationSO chatEntryConfiguration,
            ISelfProfile selfProfile)
        {
            this.view = view;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.selfProfile = selfProfile;

            view.CopyNameWarningNotification.Hide(true);
            view.CopyWalletWarningNotification.Hide(true);

            view.CopyUserNameButton.onClick.AddListener(() =>
            {
                if (currentProfile == null)
                    return;

                CopyToClipboard(currentProfile.HasClaimedName ? view.UserNameText.text : $"{currentProfile.Name}#{currentProfile.UserId[^4..]}");
                ShowCopyWarningAsync(view.CopyNameWarningNotification, CancellationToken.None).Forget();
            });
            view.CopyWalletAddressButton.onClick.AddListener(() =>
            {
                if (currentProfile == null)
                    return;

                CopyToClipboard(currentProfile.UserId);
                ShowCopyWarningAsync(view.CopyWalletWarningNotification, CancellationToken.None).Forget();
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
            view.UserWalletAddressText.text = $"{profile.UserId[..3]}...{profile.UserId[^5..]}";

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.UserNameContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.WalletAddressContainer);

            checkEditionAvailabilityCts = checkEditionAvailabilityCts.SafeRestart();
            CheckForEditionAvailabilityAsync(checkEditionAvailabilityCts.Token).Forget();
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

        private async UniTaskVoid CheckForEditionAvailabilityAsync(CancellationToken ct)
        {
            view.EditionButton.gameObject.SetActive(false);
            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile?.UserId == currentProfile.UserId)
                view.EditionButton.gameObject.SetActive(true);
        }
    }
}
