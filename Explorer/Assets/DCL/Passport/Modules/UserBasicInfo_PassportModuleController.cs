using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.ProfileElements;
using DCL.UI.ProfileNames;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleController : IPassportModuleController
    {
        private const string CLAIM_NAME_URL = "https://decentraland.org/marketplace/names/claim";

        private readonly UserNameElementController nameElementController;
        private readonly UserWalletAddressElementController walletAddressElementController;
        private readonly UserBasicInfo_PassportModuleView view;
        private readonly ISelfProfile selfProfile;
        private readonly IMVCManager mvcManager;

        private CancellationTokenSource? editNameCancellationToken;
        private Profile? currentProfile;

        public UserBasicInfo_PassportModuleController(
            UserBasicInfo_PassportModuleView view,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IMVCManager mvcManager)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            nameElementController = new UserNameElementController(view.UserNameElement);
            walletAddressElementController = new UserWalletAddressElementController(view.UserWalletAddressElement);

            view.ClaimNameButton.onClick.AddListener(() => webBrowser.OpenUrl(CLAIM_NAME_URL));
            view.EditNameButton.onClick.AddListener(ShowNameEditor);
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            nameElementController.Setup(profile);
            walletAddressElementController.Setup(profile);

            editNameCancellationToken = editNameCancellationToken.SafeRestart();
            CheckForEditionAvailabilityAsync(editNameCancellationToken.Token).Forget();
        }

        public void Clear()
        {
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
                view.EditNameButton.gameObject.SetActive(false);
                view.ClaimNameButton.gameObject.SetActive(false);

                Profile? ownProfile = await selfProfile.ProfileAsync(ct);

                if (ownProfile == null) return;

                if (ownProfile.UserId == currentProfile?.UserId)
                {
                    view.EditNameButton.gameObject.SetActive(true);
                    view.ClaimNameButton.gameObject.SetActive(!currentProfile.HasClaimedName);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
        }

        private void ShowNameEditor()
        {
            if (currentProfile == null) return;

            mvcManager.ShowAsync(EditProfileNameController.IssueCommand(new EditProfileNameParams
            {
                Profile = currentProfile,
            }));
        }
    }
}
