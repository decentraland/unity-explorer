using DCL.Passport.Configuration;
using DCL.Passport.Fields;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform MainContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text Description { get; private set; }

        [field: SerializeField]
        public TMP_InputField DescriptionForEditMode { get; private set; }

        [field: SerializeField]
        public PassportAdditionalFieldsConfigurationSO AdditionalFieldsConfiguration { get; private set; }

        [field: SerializeField]
        public RectTransform AdditionalInfoContainer { get; private set; }

        [field: SerializeField]
        public RectTransform AdditionalInfoContainerForEditMode { get; private set; }

        [field: SerializeField]
        public Link_PassportFieldView LinkPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform LinksContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text NoLinksLabel { get; private set; }

        [field: SerializeField]
        public Button InfoEditionButton { get; private set; }

        [field: SerializeField]
        public Button LinksEditionButton { get; private set; }

        [field: SerializeField]
        public Button SaveInfoButton { get; private set; }

        [field: SerializeField]
        public GameObject SaveInfoButtonLoading { get; private set; }

        [field: SerializeField]
        public Button CancelInfoButton { get; private set; }

        [field: SerializeField]
        public List<GameObject> InfoReadOnlyObjects { get; private set; }

        [field: SerializeField]
        public List<GameObject> InfoEditionObjects { get; private set; }
    }
}
