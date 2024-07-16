using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using System;
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
        private readonly PassportErrorsController passportErrorsController;

        private Profile currentProfile;
        private CancellationTokenSource checkEditionAvailabilityCts;

        public UserBasicInfo_PassportModuleController(
            UserBasicInfo_PassportModuleView view,
            ChatEntryConfigurationSO chatEntryConfiguration,
            ISelfProfile selfProfile,
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.selfProfile = selfProfile;
            this.passportErrorsController = passportErrorsController;

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
            view.UserWalletAddressText.text = $"{profile.UserId[..5]}...{profile.UserId[^5..]}";

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.UserNameContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.WalletAddressContainer);

            checkEditionAvailabilityCts = checkEditionAvailabilityCts.SafeRestart();
            // TODO (Santi): Uncomment this when the name's edition is available
            //CheckForEditionAvailabilityAsync(checkEditionAvailabilityCts.Token).Forget();
        }

        public void Clear() { }

        public void Dispose()
        {
            checkEditionAvailabilityCts.SafeCancelAndDispose();
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
            try
            {
                view.EditionButton.gameObject.SetActive(false);
                var ownProfile = await selfProfile.ProfileAsync(ct);

                if (ownProfile?.UserId == currentProfile.UserId)
                    view.EditionButton.gameObject.SetActive(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to check your profile. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
