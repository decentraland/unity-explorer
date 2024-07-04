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

        [field: Header("Info Section")]
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
        public Button InfoEditionButton { get; private set; }

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

        [field: Header("Links Section")]
        [field: SerializeField]
        public Link_PassportFieldView LinkPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform LinksContainer { get; private set; }

        [field: SerializeField]
        public RectTransform LinksContainerForEditMode { get; private set; }

        [field: SerializeField]
        public TMP_Text NoLinksLabel { get; private set; }

        [field: SerializeField]
        public Button LinksEditionButton { get; private set; }

        [field: SerializeField]
        public Button SaveLinksButton { get; private set; }

        [field: SerializeField]
        public Button AddNewLinkButton { get; private set; }

        [field: SerializeField]
        public GameObject SaveLinksButtonLoading { get; private set; }

        [field: SerializeField]
        public Button CancelLinksButton { get; private set; }

        [field: SerializeField]
        public List<GameObject> LinksReadOnlyObjects { get; private set; }

        [field: SerializeField]
        public List<GameObject> LinksEditionObjects { get; private set; }
    }
}
