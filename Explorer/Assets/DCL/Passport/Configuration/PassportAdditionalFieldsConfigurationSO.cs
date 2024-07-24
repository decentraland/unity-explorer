using DCL.Passport.Fields;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Passport.Configuration
{
    [CreateAssetMenu(fileName = "PassportAdditionalFieldsConfiguration", menuName = "SO/PassportAdditionalFieldsConfiguration")]
    public class PassportAdditionalFieldsConfigurationSO : ScriptableObject
    {
        public AdditionalField_PassportFieldView additionalInfoFieldPrefab;
        public List<AdditionalFieldConfiguration> additionalFields;
    }

    [Serializable]
    public class AdditionalFieldConfiguration
    {
        public AdditionalFieldType type;
        public string title;
        public Sprite logo;
        public PassportAdditionalFieldsValueListConfigurationSO editionValues;
    }

    public enum AdditionalFieldType
    {
        GENDER,
        COUNTRY,
        BIRTH_DATE,
        PRONOUNS,
        RELATIONSHIP_STATUS,
        SEXUAL_ORIENTATION,
        LANGUAGE,
        PROFESSION,
        EMPLOYMENT_STATUS,
        HOBBIES,
        REAL_NAME,
    }
}
