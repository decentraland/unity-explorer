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

            view.DescriptionForEditMode.onValueChanged.AddListener(UpdateCharacterCounter);
            view.DescriptionForEditMode.onSelect.AddListener(EnableEditFields);
            view.DescriptionForEditMode.onDeselect.AddListener(DisableEditFields);
        }

        private void DisableEditFields(string _)
        {
            view.DescriptionEditOutline.SetActive(false);
            view.DescriptionCharacterCounter.gameObject.SetActive(false);
        }

        private void EnableEditFields(string _)
        {
            view.DescriptionEditOutline.SetActive(true);
            view.DescriptionCharacterCounter.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            view.DescriptionForEditMode.onValueChanged.RemoveListener(UpdateCharacterCounter);
            view.DescriptionForEditMode.onSelect.RemoveListener(EnableEditFields);
            view.DescriptionForEditMode.onDeselect.RemoveListener(DisableEditFields);
        }

        public void Setup(Profile profile)
        {
            this.currentProfile = profile;
            LoadDescription();
        }

        private void LoadDescription() =>
            view.Description.text = !string.IsNullOrEmpty(currentProfile.Description) || additionalFieldsController.CurrentAdditionalFieldsCount > 0 ? currentProfile.Description : NO_INTRO_TEXT;

        public void ResetEdition() =>
            view.DescriptionForEditMode.text = view.Description.text;

        public void SaveDataIntoProfile(Profile profile) =>
            profile.Description = view.DescriptionForEditMode.text;

        public void SetAsInteractable(bool isInteractable) =>
            view.DescriptionForEditMode.interactable = isInteractable;

        private void UpdateCharacterCounter(string text) =>
            view.DescriptionCharacterCounter.text = $"{text.Length}/{view.DescriptionForEditMode.characterLimit}";
    }
}
