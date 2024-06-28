using DCL.Passport.Fields;
using TMPro;
using UnityEngine;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform MainContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text Description { get; private set; }

        [field: SerializeField]
        public PassportAdditionalFieldsConfigurationSO AdditionalFieldsConfiguration { get; private set; }

        [field: SerializeField]
        public RectTransform AdditionalInfoContainer { get; private set; }

        [field: SerializeField]
        public Link_PassportFieldView LinkPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform LinksContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text NoLinksLabel { get; private set; }
    }
}
