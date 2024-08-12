using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.ProfileElements;
using System;
using System.Threading;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleController : IPassportModuleController
    {
        private readonly UserBasicInfo_PassportModuleView view;
        private readonly ISelfProfile selfProfile;
        private readonly PassportErrorsController passportErrorsController;
        private readonly UserNameElementController nameElementController;
        private readonly UserWalletAddressElementController walletAddressElementController;

        private Profile currentProfile;
        private CancellationTokenSource checkEditionAvailabilityCts;

        public UserBasicInfo_PassportModuleController(
            UserBasicInfo_PassportModuleView view,
            ChatEntryConfigurationSO chatEntryConfiguration,
            ISelfProfile selfProfile,
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.passportErrorsController = passportErrorsController;

            nameElementController = new UserNameElementController(view.UserNameElement, chatEntryConfiguration);
            walletAddressElementController = new UserWalletAddressElementController(view.UserWalletAddressElement);
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            nameElementController.Setup(profile);
            walletAddressElementController.Setup(profile);

            checkEditionAvailabilityCts = checkEditionAvailabilityCts.SafeRestart();
            // TODO (Santi): Uncomment this when the name's edition is available
            //CheckForEditionAvailabilityAsync(checkEditionAvailabilityCts.Token).Forget();
        }

        public void Clear()
        {
            checkEditionAvailabilityCts.SafeCancelAndDispose();
            nameElementController.Element.CopyNameWarningNotification.Hide(true);
            walletAddressElementController.Element.CopyWalletWarningNotification.Hide(true);
        }

        public void Dispose()
        {
            nameElementController.Element.CopyUserNameButton.onClick.RemoveAllListeners();
            walletAddressElementController.Element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            Clear();
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
