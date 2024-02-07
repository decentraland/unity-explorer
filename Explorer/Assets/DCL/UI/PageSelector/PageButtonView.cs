using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class PageButtonView : MonoBehaviour
    {
        [field: SerializeField]
        public Button PageButton { get; private set; }

        [field: SerializeField]
        public TMP_Text PageText { get; private set; }

        [field: SerializeField]
        public GameObject SelectedBackground { get; private set; }

        [field: SerializeField]
        public int PageIndex { get; set; }
    }
}
