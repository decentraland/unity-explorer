using DCL.Profiles;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleController : IPassportModuleController
    {
        private const string NO_INTRO_TEXT = "No intro.";
        private const int ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY = 11;

        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly IObjectPool<AdditionalField_PassportFieldView> additionalFieldsPool;
        private readonly List<AdditionalField_PassportFieldView> instantiatedAdditionalFields = new();

        private Profile currentProfile;

        public UserDetailedInfo_PassportModuleController(UserDetailedInfo_PassportModuleView view)
        {
            this.view = view;

            additionalFieldsPool = new ObjectPool<AdditionalField_PassportFieldView>(
                InstantiateAdditionalFieldPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            view.Description.text = !string.IsNullOrEmpty(profile.Description) ? profile.Description : NO_INTRO_TEXT;
            LoadAdditionalFields();

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
        }

        public void Clear()
        {
            foreach (AdditionalField_PassportFieldView additionalField in instantiatedAdditionalFields)
                additionalFieldsPool.Release(additionalField);

            instantiatedAdditionalFields.Clear();
        }

        public void Dispose() =>
            Clear();

        private AdditionalField_PassportFieldView InstantiateAdditionalFieldPrefab()
        {
            AdditionalField_PassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainer);
            return additionalFieldView;
        }

        private void LoadAdditionalFields()
        {
            if (!string.IsNullOrEmpty(currentProfile.Gender))
                AddAdditionalField(AdditionalFieldType.GENDER, currentProfile.Gender);

            if (!string.IsNullOrEmpty(currentProfile.Country))
                AddAdditionalField(AdditionalFieldType.COUNTRY, currentProfile.Country);

            if (currentProfile.Birthdate != null && currentProfile.Birthdate.Value != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"));

            if (!string.IsNullOrEmpty(currentProfile.Pronouns))
                AddAdditionalField(AdditionalFieldType.PRONOUNS, currentProfile.Pronouns);

            if (!string.IsNullOrEmpty(currentProfile.RelationshipStatus))
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, currentProfile.RelationshipStatus);

            if (!string.IsNullOrEmpty(currentProfile.SexualOrientation))
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, currentProfile.SexualOrientation);

            if (!string.IsNullOrEmpty(currentProfile.Language))
                AddAdditionalField(AdditionalFieldType.LANGUAGE, currentProfile.Language);

            if (!string.IsNullOrEmpty(currentProfile.Profession))
                AddAdditionalField(AdditionalFieldType.PROFESSION, currentProfile.Profession);

            if (!string.IsNullOrEmpty(currentProfile.EmploymentStatus))
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, currentProfile.EmploymentStatus);

            if (!string.IsNullOrEmpty(currentProfile.Hobbies))
                AddAdditionalField(AdditionalFieldType.HOBBIES, currentProfile.Hobbies);

            if (!string.IsNullOrEmpty(currentProfile.RealName))
                AddAdditionalField(AdditionalFieldType.REAL_NAME, currentProfile.RealName);
        }

        private void AddAdditionalField(AdditionalFieldType type, string value)
        {
            var newAdditionalField = additionalFieldsPool.Get();
            newAdditionalField.transform.parent = view.AdditionalInfoContainer;
            newAdditionalField.transform.SetAsLastSibling();
            newAdditionalField.Value.text = value;
            newAdditionalField.Title.text = type.ToString();
            newAdditionalField.Logo.sprite = null;

            foreach (AdditionalFieldConfiguration additionalFieldConfig in view.AdditionalFieldsConfiguration.additionalFields)
            {
                if (additionalFieldConfig.type != type)
                    continue;

                newAdditionalField.Title.text = additionalFieldConfig.title;
                newAdditionalField.Logo.sprite = additionalFieldConfig.logo;
            }

            instantiatedAdditionalFields.Add(newAdditionalField);
        }
    }
}
