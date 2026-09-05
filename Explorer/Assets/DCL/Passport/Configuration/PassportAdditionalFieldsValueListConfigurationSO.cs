using UnityEngine;

namespace DCL.Passport.Configuration
{
    [CreateAssetMenu(fileName = "PassportAdditionalFieldsValueListConfiguration", menuName = "DCL/Passport/Passport Additional Fields Value List Configuration")]
    public class PassportAdditionalFieldsValueListConfigurationSO : ScriptableObject
    {
        public string[] values;
    }
}
