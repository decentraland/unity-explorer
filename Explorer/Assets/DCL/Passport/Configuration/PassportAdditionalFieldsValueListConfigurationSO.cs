using UnityEngine;

namespace DCL.Passport.Configuration
{
    [CreateAssetMenu(fileName = "PassportAdditionalFieldsValueListConfiguration", menuName = "SO/PassportAdditionalFieldsValueListConfiguration")]
    public class PassportAdditionalFieldsValueListConfigurationSO : ScriptableObject
    {
        public string[] values;
    }
}
