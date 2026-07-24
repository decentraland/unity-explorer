using DCL.Passport.Configuration;
using DCL.Passport.Fields;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine.Pool;

namespace DCL.Passport.Modules
{
    public class UserAdditionalFieldsPassportSubModuleController
    {
        private const int ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY = 11;
        private const string EDITION_DROPDOWN_DEFAULT_OPTION = "Select";
        private const string EDITION_PLACE_HOLDER = "Write here";
        private const string EDITION_PLACE_HOLDER_FOR_DATES = "DD/MM/YYYY";

        private readonly UserDetailedInfoPassportModuleView view;

        private Profile currentProfile;
        private readonly IObjectPool<AdditionalFieldPassportFieldView> additionalFieldsPool;
        private readonly List<AdditionalFieldPassportFieldView> instantiatedAdditionalFields = new ();
        private readonly IObjectPool<AdditionalFieldPassportFieldView> additionalFieldsPoolForEdition;
        private readonly List<AdditionalFieldPassportFieldView> instantiatedAdditionalFieldsForEdition = new ();

        private readonly string[] validInputFormatsForDate = { "dd/MM/yyyy", "ddMMyyyy" };

        public int CurrentAdditionalFieldsCount => instantiatedAdditionalFields.Count;

        public UserAdditionalFieldsPassportSubModuleController(UserDetailedInfoPassportModuleView view)
        {
            this.view = view;

            additionalFieldsPool = new ObjectPool<AdditionalFieldPassportFieldView>(
                InstantiateAdditionalFieldPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false));

            additionalFieldsPoolForEdition = new ObjectPool<AdditionalFieldPassportFieldView>(
                InstantiateAdditionalFieldForEditionPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false));
        }

        public void Setup(Profile profile)
        {
            this.currentProfile = profile;
            LoadAdditionalFields();
        }

        private AdditionalFieldPassportFieldView InstantiateAdditionalFieldPrefab()
        {
            AdditionalFieldPassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainer);
            return additionalFieldView;
        }

        private AdditionalFieldPassportFieldView InstantiateAdditionalFieldForEditionPrefab()
        {
            AdditionalFieldPassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainerForEditMode);
            return additionalFieldView;
        }

        public void ClearAllAdditionalInfoFields()
        {
            ClearAdditionalInfoFields();
            ClearAdditionalInfoFieldsForEdition();
        }

        private void ClearAdditionalInfoFields()
        {
            foreach (AdditionalFieldPassportFieldView additionalField in instantiatedAdditionalFields)
                additionalFieldsPool.Release(additionalField);

            instantiatedAdditionalFields.Clear();
        }

        private void ClearAdditionalInfoFieldsForEdition()
        {
            foreach (AdditionalFieldPassportFieldView additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
                additionalFieldsPoolForEdition.Release(additionalFieldForEdition);

            instantiatedAdditionalFieldsForEdition.Clear();
        }

        private void LoadAdditionalFields()
        {
            if (!string.IsNullOrEmpty(currentProfile.Gender))
            {
                AddAdditionalField(AdditionalFieldType.Gender, currentProfile.Gender, false);
                AddAdditionalField(AdditionalFieldType.Gender, currentProfile.Gender, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.Gender, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Country))
            {
                AddAdditionalField(AdditionalFieldType.Country, currentProfile.Country, false);
                AddAdditionalField(AdditionalFieldType.Country, currentProfile.Country, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.Country, string.Empty, true);

            if (currentProfile.Birthdate != null && currentProfile.Birthdate.Value != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            {
                AddAdditionalField(AdditionalFieldType.BirthDate, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"), false);
                AddAdditionalField(AdditionalFieldType.BirthDate, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"), true);
            }
            else
                AddAdditionalField(AdditionalFieldType.BirthDate, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Pronouns))
            {
                AddAdditionalField(AdditionalFieldType.Pronouns, currentProfile.Pronouns, false);
                AddAdditionalField(AdditionalFieldType.Pronouns, currentProfile.Pronouns, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.Pronouns, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.RelationshipStatus))
            {
                AddAdditionalField(AdditionalFieldType.RelationshipStatus, currentProfile.RelationshipStatus, false);
                AddAdditionalField(AdditionalFieldType.RelationshipStatus, currentProfile.RelationshipStatus, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.RelationshipStatus, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.SexualOrientation))
            {
                AddAdditionalField(AdditionalFieldType.SexualOrientation, currentProfile.SexualOrientation, false);
                AddAdditionalField(AdditionalFieldType.SexualOrientation, currentProfile.SexualOrientation, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.SexualOrientation, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Language))
            {
                AddAdditionalField(AdditionalFieldType.Language, currentProfile.Language, false);
                AddAdditionalField(AdditionalFieldType.Language, currentProfile.Language, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.Language, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Profession))
            {
                AddAdditionalField(AdditionalFieldType.Profession, currentProfile.Profession, false);
                AddAdditionalField(AdditionalFieldType.Profession, currentProfile.Profession, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.Profession, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.EmploymentStatus))
            {
                AddAdditionalField(AdditionalFieldType.EmploymentStatus, currentProfile.EmploymentStatus, false);
                AddAdditionalField(AdditionalFieldType.EmploymentStatus, currentProfile.EmploymentStatus, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.EmploymentStatus, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Hobbies))
            {
                AddAdditionalField(AdditionalFieldType.Hobbies, currentProfile.Hobbies, false);
                AddAdditionalField(AdditionalFieldType.Hobbies, currentProfile.Hobbies, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.Hobbies, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.RealName))
            {
                AddAdditionalField(AdditionalFieldType.RealName, currentProfile.RealName, false);
                AddAdditionalField(AdditionalFieldType.RealName, currentProfile.RealName, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.RealName, string.Empty, true);

            view.AdditionalInfoContainer.gameObject.SetActive(instantiatedAdditionalFields.Count > 0);
        }

        private void AddAdditionalField(AdditionalFieldType type, string value, bool isEditMode)
        {
            var newAdditionalField = !isEditMode ? additionalFieldsPool.Get() : additionalFieldsPoolForEdition.Get();
            newAdditionalField.transform.SetAsLastSibling();
            newAdditionalField.Value.text = value;
            newAdditionalField.Type = type;
            newAdditionalField.Title.text = type.ToString();
            newAdditionalField.Logo.sprite = null;
            newAdditionalField.EditionDropdown.options.Clear();
            newAdditionalField.EditionDropdown.options.Add(new TMP_Dropdown.OptionData { text = EDITION_DROPDOWN_DEFAULT_OPTION });
            newAdditionalField.EditionTextInput.text = string.Empty;
            newAdditionalField.EditionTextInputPlaceholder.text = type == AdditionalFieldType.BirthDate ? EDITION_PLACE_HOLDER_FOR_DATES : EDITION_PLACE_HOLDER;

            foreach (AdditionalFieldConfiguration additionalFieldConfig in view.AdditionalFieldsConfiguration.additionalFields)
            {
                if (additionalFieldConfig.type != type)
                    continue;

                newAdditionalField.Title.text = additionalFieldConfig.title;
                newAdditionalField.Logo.sprite = additionalFieldConfig.logo;
                newAdditionalField.IsEditableWithDropdown = additionalFieldConfig.editionValues != null;

                if (additionalFieldConfig.editionValues != null)
                    foreach (string option in additionalFieldConfig.editionValues.values)
                        newAdditionalField.EditionDropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
            }

            newAdditionalField.SetAsEditable(isEditMode);

            if (!isEditMode)
                instantiatedAdditionalFields.Add(newAdditionalField);
            else
                instantiatedAdditionalFieldsForEdition.Add(newAdditionalField);
        }

        public void ResetEdition()
        {
            foreach (var additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
            {
                additionalFieldForEdition.SetEditionValue(string.Empty);
                foreach (var additionalField in instantiatedAdditionalFields)
                {
                    if (additionalFieldForEdition.Type != additionalField.Type)
                        continue;

                    additionalFieldForEdition.SetEditionValue(additionalField.Value.text);
                    break;
                }
            }
        }

        public void SaveDataIntoProfile(Profile profile)
        {
            foreach (var additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
            {
                string? valueToSave = !string.IsNullOrEmpty(additionalFieldForEdition.EditionTextInput.text) ? additionalFieldForEdition.EditionTextInput.text : null;
                switch (additionalFieldForEdition.Type)
                {
                    case AdditionalFieldType.Gender:
                        profile.Gender = valueToSave;
                        break;
                    case AdditionalFieldType.Country:
                        profile.Country = valueToSave;
                        break;
                    case AdditionalFieldType.BirthDate:
                        if (valueToSave != null)
                            profile.Birthdate = DateTime.SpecifyKind(DateTime.ParseExact(valueToSave, validInputFormatsForDate, CultureInfo.InvariantCulture, DateTimeStyles.None), DateTimeKind.Utc);
                        else
                            profile.Birthdate = null;
                        break;
                    case AdditionalFieldType.Pronouns:
                        profile.Pronouns = valueToSave;
                        break;
                    case AdditionalFieldType.RelationshipStatus:
                        profile.RelationshipStatus = valueToSave;
                        break;
                    case AdditionalFieldType.SexualOrientation:
                        profile.SexualOrientation = valueToSave;
                        break;
                    case AdditionalFieldType.Language:
                        profile.Language = valueToSave;
                        break;
                    case AdditionalFieldType.Profession:
                        profile.Profession = valueToSave;
                        break;
                    case AdditionalFieldType.EmploymentStatus:
                        profile.EmploymentStatus = valueToSave;
                        break;
                    case AdditionalFieldType.Hobbies:
                        profile.Hobbies = valueToSave;
                        break;
                    case AdditionalFieldType.RealName:
                        profile.RealName = valueToSave;
                        break;
                }
            }
        }

        public void SetAsInteractable(bool isInteractable)
        {
            foreach (var additionalInfoForEdition in instantiatedAdditionalFieldsForEdition)
                additionalInfoForEdition.SetAsInteractable(isInteractable);
        }
    }
}
