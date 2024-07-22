using DCL.Profiles;

namespace DCL.Passport.Modules
{
    public class UserDescription_PassportSubModuleController
    {
        private const string NO_INTRO_TEXT = "No intro.";

        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly UserAdditionalFields_PassportSubModuleController additionalFieldsController;

        private Profile currentProfile;

        public UserDescription_PassportSubModuleController(
            UserDetailedInfo_PassportModuleView view,
            UserAdditionalFields_PassportSubModuleController additionalFieldsController)
        {
            this.view = view;
            this.additionalFieldsController = additionalFieldsController;
        }

        public void Setup(Profile profile) =>
            this.currentProfile = profile;

        public void LoadDescription() =>
            view.Description.text = !string.IsNullOrEmpty(currentProfile.Description) || additionalFieldsController.CurrentAdditionalFieldsCount > 0 ? currentProfile.Description : NO_INTRO_TEXT;

        public void ResetEdition() =>
            view.DescriptionForEditMode.text = view.Description.text;

        public void SaveDataIntoProfile() =>
            currentProfile.Description = view.DescriptionForEditMode.text;

        public void SetAsInteractable(bool isInteractable) =>
            view.DescriptionForEditMode.interactable = isInteractable;
    }
}
