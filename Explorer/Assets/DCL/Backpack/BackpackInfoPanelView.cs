using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class BackpackInfoPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public Image WearableThumbnail { get; private set; }

        [field: SerializeField]
        public Image CategoryImage { get; private set; }

        [field: SerializeField]
        public Image RarityBackground { get; private set; }

        [field: SerializeField]
        public TMP_Text Name { get; private set; }

        [field: SerializeField]
        public TMP_Text Description { get; private set; }
    }
}
