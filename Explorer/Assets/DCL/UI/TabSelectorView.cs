using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class TabSelectorView : MonoBehaviour
    {
        [field: SerializeField]
        public Toggle TabSelectorToggle { get; private set; }

        [field: SerializeField]
        public Image SelectedBackground { get; private set; }

        [field: SerializeField]
        public Image UnselectedImage { get; private set; }

        [field: SerializeField]
        public Image SelectedImage { get; private set; }

        [field: SerializeField]
        public GameObject UnselectedText { get; private set; }

        [field: SerializeField]
        public GameObject SelectedText { get; private set; }
    }
}
