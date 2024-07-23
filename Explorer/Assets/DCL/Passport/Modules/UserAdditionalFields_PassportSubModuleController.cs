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
    public class UserAdditionalFields_PassportSubModuleController
    {
        private const int ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY = 11;
        private const string EDITION_DROPDOWN_DEFAULT_OPTION = "-Select an option-";
        private const string EDITION_PLACE_HOLDER = "Write here";
        private const string EDITION_PLACE_HOLDER_FOR_DATES = "DD/MM/YYYY";

        private readonly UserDetailedInfo_PassportModuleView view;

        private Profile currentProfile;
        private readonly IObjectPool<AdditionalField_PassportFieldView> additionalFieldsPool;
        private readonly List<AdditionalField_PassportFieldView> instantiatedAdditionalFields = new ();
        private readonly IObjectPool<AdditionalField_PassportFieldView> additionalFieldsPoolForEdition;
        private readonly List<AdditionalField_PassportFieldView> instantiatedAdditionalFieldsForEdition = new ();

        public int CurrentAdditionalFieldsCount => instantiatedAdditionalFields.Count;

        public UserAdditionalFields_PassportSubModuleController(UserDetailedInfo_PassportModuleView view)
        {
            this.view = view;

            additionalFieldsPool = new ObjectPool<AdditionalField_PassportFieldView>(
                InstantiateAdditionalFieldPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false));

            additionalFieldsPoolForEdition = new ObjectPool<AdditionalField_PassportFieldView>(
                InstantiateAdditionalFieldForEditionPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false));
        }

        public void Setup(Profile profile) =>
            this.currentProfile = profile;

        private AdditionalField_PassportFieldView InstantiateAdditionalFieldPrefab()
        {
            AdditionalField_PassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainer);
            return additionalFieldView;
        }

        private AdditionalField_PassportFieldView InstantiateAdditionalFieldForEditionPrefab()
        {
            AdditionalField_PassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainerForEditMode);
            return additionalFieldView;
        }

        public void ClearAllAdditionalInfoFields()
        {
            ClearAdditionalInfoFields();
            ClearAdditionalInfoFieldsForEdition();
        }

        private void ClearAdditionalInfoFields()
        {
            foreach (AdditionalField_PassportFieldView additionalField in instantiatedAdditionalFields)
                additionalFieldsPool.Release(additionalField);

            instantiatedAdditionalFields.Clear();
        }

        private void ClearAdditionalInfoFieldsForEdition()
        {
            foreach (AdditionalField_PassportFieldView additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
                additionalFieldsPoolForEdition.Release(additionalFieldForEdition);

            instantiatedAdditionalFieldsForEdition.Clear();
        }

        public void LoadAdditionalFields()
        {
            if (!string.IsNullOrEmpty(currentProfile.Gender))
            {
                AddAdditionalField(AdditionalFieldType.GENDER, currentProfile.Gender, false);
                AddAdditionalField(AdditionalFieldType.GENDER, currentProfile.Gender, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.GENDER, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Country))
            {
                AddAdditionalField(AdditionalFieldType.COUNTRY, currentProfile.Country, false);
                AddAdditionalField(AdditionalFieldType.COUNTRY, currentProfile.Country, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.COUNTRY, string.Empty, true);

            if (currentProfile.Birthdate != null && currentProfile.Birthdate.Value != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            {
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"), false);
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"), true);
            }
            else
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Pronouns))
            {
                AddAdditionalField(AdditionalFieldType.PRONOUNS, currentProfile.Pronouns, false);
                AddAdditionalField(AdditionalFieldType.PRONOUNS, currentProfile.Pronouns, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.PRONOUNS, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.RelationshipStatus))
            {
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, currentProfile.RelationshipStatus, false);
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, currentProfile.RelationshipStatus, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.SexualOrientation))
            {
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, currentProfile.SexualOrientation, false);
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, currentProfile.SexualOrientation, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Language))
            {
                AddAdditionalField(AdditionalFieldType.LANGUAGE, currentProfile.Language, false);
                AddAdditionalField(AdditionalFieldType.LANGUAGE, currentProfile.Language, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.LANGUAGE, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Profession))
            {
                AddAdditionalField(AdditionalFieldType.PROFESSION, currentProfile.Profession, false);
                AddAdditionalField(AdditionalFieldType.PROFESSION, currentProfile.Profession, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.PROFESSION, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.EmploymentStatus))
            {
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, currentProfile.EmploymentStatus, false);
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, currentProfile.EmploymentStatus, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Hobbies))
            {
                AddAdditionalField(AdditionalFieldType.HOBBIES, currentProfile.Hobbies, false);
                AddAdditionalField(AdditionalFieldType.HOBBIES, currentProfile.Hobbies, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.HOBBIES, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.RealName))
            {
                AddAdditionalField(AdditionalFieldType.REAL_NAME, currentProfile.RealName, false);
                AddAdditionalField(AdditionalFieldType.REAL_NAME, currentProfile.RealName, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.REAL_NAME, string.Empty, true);

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
            newAdditionalField.EditionTextInputPlaceholder.text = type == AdditionalFieldType.BIRTH_DATE ? EDITION_PLACE_HOLDER_FOR_DATES : EDITION_PLACE_HOLDER;

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

        public void SaveDataIntoProfile()
        {
            foreach (var additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
            {
                string valueToSave = !string.IsNullOrEmpty(additionalFieldForEdition.EditionTextInput.text) ? additionalFieldForEdition.EditionTextInput.text : null;
                switch (additionalFieldForEdition.Type)
                {
                    case AdditionalFieldType.GENDER:
                        currentProfile.Gender = valueToSave;
                        break;
                    case AdditionalFieldType.COUNTRY:
                        currentProfile.Country = valueToSave;
                        break;
                    case AdditionalFieldType.BIRTH_DATE:
                        if (valueToSave != null)
                            currentProfile.Birthdate = DateTime.ParseExact(valueToSave, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                        else
                            currentProfile.Birthdate = null;
                        break;
                    case AdditionalFieldType.PRONOUNS:
                        currentProfile.Pronouns = valueToSave;
                        break;
                    case AdditionalFieldType.RELATIONSHIP_STATUS:
                        currentProfile.RelationshipStatus = valueToSave;
                        break;
                    case AdditionalFieldType.SEXUAL_ORIENTATION:
                        currentProfile.SexualOrientation = valueToSave;
                        break;
                    case AdditionalFieldType.LANGUAGE:
                        currentProfile.Language = valueToSave;
                        break;
                    case AdditionalFieldType.PROFESSION:
                        currentProfile.Profession = valueToSave;
                        break;
                    case AdditionalFieldType.EMPLOYMENT_STATUS:
                        currentProfile.EmploymentStatus = valueToSave;
                        break;
                    case AdditionalFieldType.HOBBIES:
                        currentProfile.Hobbies = valueToSave;
                        break;
                    case AdditionalFieldType.REAL_NAME:
                        currentProfile.RealName = valueToSave;
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
