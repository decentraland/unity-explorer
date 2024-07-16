using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Passport.Modals;
using DCL.Profiles;
using DCL.Profiles.Self;
using MVC;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleController : IPassportModuleController
    {
        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly ISelfProfile selfProfile;
        private readonly IProfileRepository profileRepository;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly PassportErrorsController passportErrorsController;
        private readonly UserAdditionalFields_PassportSubModuleController additionalFieldsController;
        private readonly UserDescription_PassportSubModuleController descriptionController;
        private readonly UserLinks_PassportSubModuleController linksController;

        private Profile currentProfile;
        private CancellationTokenSource checkEditionAvailabilityCts;
        private CancellationTokenSource saveInfoCts;

        public UserDetailedInfo_PassportModuleController(
            UserDetailedInfo_PassportModuleView view,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            IProfileRepository profileRepository,
            World world,
            Entity playerEntity,
            AddLink_PassportModal addLinkModal,
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.profileRepository = profileRepository;
            this.world = world;
            this.playerEntity = playerEntity;
            this.passportErrorsController = passportErrorsController;

            additionalFieldsController = new UserAdditionalFields_PassportSubModuleController(view);
            descriptionController = new UserDescription_PassportSubModuleController(view, additionalFieldsController);
            linksController = new UserLinks_PassportSubModuleController(view, addLinkModal, mvcManager, profileRepository, world, playerEntity, passportErrorsController);

            view.InfoEditionButton.onClick.AddListener(() => SetInfoSectionAsEditionMode(true));
            view.CancelInfoButton.onClick.AddListener(() => SetInfoSectionAsEditionMode(false));
            view.SaveInfoButton.onClick.AddListener(SaveInfoSection);
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            additionalFieldsController.Setup(profile);
            descriptionController.Setup(profile);
            linksController.Setup(profile);

            SetInfoSectionAsEditionMode(false);
            additionalFieldsController.LoadAdditionalFields();
            descriptionController.LoadDescription();
            linksController.SetLinksSectionAsEditionMode(false);
            linksController.LoadLinks();

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);

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

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
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
                await UpdateProfileAsync(ct);
                additionalFieldsController.ClearAllAdditionalInfoFields();
                additionalFieldsController.LoadAdditionalFields();
                descriptionController.LoadDescription();
                SetInfoSectionAsEditionMode(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
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

        private async UniTask UpdateProfileAsync(CancellationToken ct)
        {
            try
            {
                // Update profile data
                await profileRepository.SetAsync(currentProfile, ct);

                // Update player entity in world
                currentProfile.IsDirty = true;
                world.Set(playerEntity, currentProfile);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to update your profile info. Please try again!";
                passportErrorsController.Show();
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
            finally
            {
                if (currentProfile != null)
                    currentProfile = await profileRepository.GetAsync(currentProfile.UserId, 0, ct);
            }
        }
    }
}
