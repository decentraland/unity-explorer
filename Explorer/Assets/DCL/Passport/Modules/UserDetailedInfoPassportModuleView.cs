using DCL.Passport.Configuration;
using DCL.Passport.Fields;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfoPassportModuleView : MonoBehaviour
    {
        [field: Header("Info Section")]
        [field: SerializeField]
        public TMP_Text Description { get; private set; } = null!;

        [field: SerializeField]
        public TMP_InputField DescriptionForEditMode { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text DescriptionCharacterCounter { get; private set; } = null!;

        [field: SerializeField]
        public GameObject DescriptionEditOutline { get; private set; } = null!;

        [field: SerializeField]
        public PassportAdditionalFieldsConfigurationSO AdditionalFieldsConfiguration { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform AdditionalInfoContainer { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform AdditionalInfoContainerForEditMode { get; private set; } = null!;

        [field: SerializeField]
        public Button InfoEditionButton { get; private set; } = null!;

        [field: SerializeField]
        public Button SaveInfoButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject SaveInfoButtonLoading { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelInfoButton { get; private set; } = null!;

        [field: SerializeField]
        public List<GameObject> InfoReadOnlyObjects { get; private set; } = null!;

        [field: SerializeField]
        public List<GameObject> InfoEditionObjects { get; private set; } = null!;

        [field: Header("Links Section")]
        [field: SerializeField]
        public LinkPassportFieldView LinkPrefab { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform LinksContainer { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform LinksContainerForEditMode { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text NoLinksLabel { get; private set; } = null!;

        [field: SerializeField]
        public Button LinksEditionButton { get; private set; } = null!;

        [field: SerializeField]
        public Button SaveLinksButton { get; private set; } = null!;

        [field: SerializeField]
        public Button AddNewLinkButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject SaveLinksButtonLoading { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelLinksButton { get; private set; } = null!;

        [field: SerializeField]
        public List<GameObject> LinksReadOnlyObjects { get; private set; } = null!;

        [field: SerializeField]
        public List<GameObject> LinksEditionObjects { get; private set; } = null!;

        [field: Header("Others")]
        [field: SerializeField]
        public List<Button> ButtonsToDisableWhileSaving { get; private set; } = null!;
    }
}
