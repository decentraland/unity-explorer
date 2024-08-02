using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Passport.Modals;
using DCL.Profiles;
using DCL.Profiles.Self;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleController : IPassportModuleController
    {
        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly ISelfProfile selfProfile;
        private readonly PassportErrorsController passportErrorsController;
        private readonly UserAdditionalFields_PassportSubModuleController additionalFieldsController;
        private readonly UserDescription_PassportSubModuleController descriptionController;
        private readonly UserLinks_PassportSubModuleController linksController;
        private readonly PassportProfileInfoController passportProfileInfoController;

        private Profile currentProfile;
        private CancellationTokenSource checkEditionAvailabilityCts;
        private CancellationTokenSource saveInfoCts;

        public UserDetailedInfo_PassportModuleController(
            UserDetailedInfo_PassportModuleView view,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            AddLink_PassportModal addLinkModal,
            PassportErrorsController passportErrorsController,
            PassportProfileInfoController passportProfileInfoController)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.passportErrorsController = passportErrorsController;
            this.passportProfileInfoController = passportProfileInfoController;

            additionalFieldsController = new UserAdditionalFields_PassportSubModuleController(view);
            descriptionController = new UserDescription_PassportSubModuleController(view, additionalFieldsController);
            linksController = new UserLinks_PassportSubModuleController(view, addLinkModal, mvcManager, passportProfileInfoController);

            view.InfoEditionButton.onClick.AddListener(() => SetInfoSectionAsEditionMode(true));
            view.CancelInfoButton.onClick.AddListener(() => SetInfoSectionAsEditionMode(false));
            view.SaveInfoButton.onClick.AddListener(SaveInfoSection);
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            Clear();
            additionalFieldsController.Setup(profile);
            descriptionController.Setup(profile);
            linksController.Setup(profile);
            SetInfoSectionAsEditionMode(false);
            checkEditionAvailabilityCts = checkEditionAvailabilityCts.SafeRestart();
            CheckForEditionAvailabilityAsync(checkEditionAvailabilityCts.Token).Forget();
        }

        public void Clear()
        {
            additionalFieldsController.ClearAllAdditionalInfoFields();
            linksController.ClearAllLinks();
        }

        public void Dispose()
        {
            Clear();
            view.InfoEditionButton.onClick.RemoveAllListeners();
            view.CancelInfoButton.onClick.RemoveAllListeners();
            view.SaveInfoButton.onClick.RemoveAllListeners();
            checkEditionAvailabilityCts.SafeCancelAndDispose();
            saveInfoCts.SafeCancelAndDispose();
            linksController.Dispose();
        }

        private async UniTaskVoid CheckForEditionAvailabilityAsync(CancellationToken ct)
        {
            try
            {
                view.InfoEditionButton.gameObject.SetActive(false);
                linksController.SetLinksEditionButtonAsActive(false);
                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile?.UserId == currentProfile.UserId)
                {
                    view.InfoEditionButton.gameObject.SetActive(true);
                    linksController.SetLinksEditionButtonAsActive(true);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to check your profile. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void SetInfoSectionAsEditionMode(bool isEditMode)
        {
            SetInfoSectionAsSavingStatus(false);

            foreach (var editionObj in view.InfoEditionObjects)
                editionObj.SetActive(isEditMode);

            foreach (var readOnlyObj in view.InfoReadOnlyObjects)
                readOnlyObj.SetActive(!isEditMode);

            additionalFieldsController.ResetEdition();

            if (isEditMode)
                descriptionController.ResetEdition();
            else
            {
                view.AdditionalInfoContainer.gameObject.SetActive(additionalFieldsController.CurrentAdditionalFieldsCount > 0);
                saveInfoCts.SafeCancelAndDispose();
            }
        }

        private void SaveInfoSection()
        {
            saveInfoCts = saveInfoCts.SafeRestart();
            SaveInfoAsync(saveInfoCts.Token).Forget();
            return;

            async UniTaskVoid SaveInfoAsync(CancellationToken ct)
            {
                SetInfoSectionAsSavingStatus(true);
                descriptionController.SaveDataIntoProfile();
                additionalFieldsController.SaveDataIntoProfile();
                await passportProfileInfoController.UpdateProfileAsync(ct);
            }
        }

        private void SetInfoSectionAsSavingStatus(bool isSaving)
        {
            view.SaveInfoButtonLoading.SetActive(isSaving);
            view.CancelInfoButton.gameObject.SetActive(!isSaving);
            view.SaveInfoButton.gameObject.SetActive(!isSaving);
            descriptionController.SetAsInteractable(!isSaving);
            linksController.SetSaveButtonAsInteractable(!isSaving);
            additionalFieldsController.SetAsInteractable(!isSaving);
        }
    }
}
