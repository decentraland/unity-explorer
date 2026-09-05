using DCL.Passport.Fields;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Passport.Configuration
{
    [CreateAssetMenu(fileName = "PassportAdditionalFieldsConfiguration", menuName = "DCL/Passport/Passport Additional Fields Configuration")]
    public class PassportAdditionalFieldsConfigurationSO : ScriptableObject
    {
        public AdditionalFieldPassportFieldView additionalInfoFieldPrefab;
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
        Gender,
        Country,
        BirthDate,
        Pronouns,
        RelationshipStatus,
        SexualOrientation,
        Language,
        Profession,
        EmploymentStatus,
        Hobbies,
        RealName,
    }
}
