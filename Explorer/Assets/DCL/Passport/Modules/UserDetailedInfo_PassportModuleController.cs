using DCL.Profiles;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleController : IDisposable
    {
        private const string NO_INTRO_TEXT = "No intro.";
        private const int MAX_CONCURRENT_ADDITIONAL_FIELDS = 11;

        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly IObjectPool<AdditionalField_PassportFieldView> additionalFieldsPool;
        private readonly List<AdditionalField_PassportFieldView> instantiatedAdditionalFields = new();

        private Profile currentProfile;

        public UserDetailedInfo_PassportModuleController(UserDetailedInfo_PassportModuleView view)
        {
            this.view = view;

            additionalFieldsPool = new ObjectPool<AdditionalField_PassportFieldView>(
                InstantiateAdditionalField,
                defaultCapacity: MAX_CONCURRENT_ADDITIONAL_FIELDS,
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

        public void ClearAdditionalFields()
        {
            foreach (AdditionalField_PassportFieldView additionalField in instantiatedAdditionalFields)
                additionalFieldsPool.Release(additionalField);

            instantiatedAdditionalFields.Clear();
        }

        public void Dispose()
        {
            ClearAdditionalFields();
        }

        private AdditionalField_PassportFieldView InstantiateAdditionalField()
        {
            AdditionalField_PassportFieldView backpackItemView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainer);
            return backpackItemView;
        }

        private void LoadAdditionalFields()
        {
            if (!string.IsNullOrEmpty(currentProfile.Gender))
                AddAdditionalField(AdditionalFieldType.GENDER, currentProfile.Gender);

            if (!string.IsNullOrEmpty(currentProfile.Country))
                AddAdditionalField(AdditionalFieldType.COUNTRY, currentProfile.Country);

            // TODO: Add more fields here...

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.AdditionalInfoContainer);
        }

        private void AddAdditionalField(AdditionalFieldType type, string value)
        {
            var newAdditionalField = additionalFieldsPool.Get();
            newAdditionalField.transform.parent = view.AdditionalInfoContainer;
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
