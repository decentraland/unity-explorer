using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class BackpackInfoPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public HideCategoryGridView HideCategoryGridView { get; private set; }

        [field: SerializeField]
        public GameObject EmptyPanel { get; private set; }

        [field: SerializeField]
        public GameObject FullPanel { get; private set; }

        [field: SerializeField]
        public Image WearableThumbnail { get; private set; }

        [field: SerializeField]
        public Image RarityBackgroundPanel { get; private set; }

        [field: SerializeField]
        public TMP_Text RarityName { get; private set; }

        [field: SerializeField]
        public GameObject ThirdPartyRarityBackgroundPanel { get; private set; }

        [field: SerializeField]
        public TMP_Text ThirdPartyCollectionName { get; private set; }

        [field: SerializeField]
        public GameObject ThirdPartyCollectionContainer { get; private set; }

        [field: SerializeField]
        public Image CategoryImage { get; private set; }

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; }

        [field: SerializeField]
        public Image RarityBackground { get; private set; }

        [field: SerializeField]
        public TMP_Text Name { get; private set; }

        [field: SerializeField]
        public TMP_Text Description { get; private set; }
    }
}
