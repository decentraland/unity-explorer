using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ToggleView : MonoBehaviour
    {
        [field: SerializeField]
        public Toggle Toggle { get; private set; }

        [field: SerializeField]
        public GameObject OnImage { get; private set; }

        [field: SerializeField]
        public GameObject OffImage { get; private set; }

        [field: SerializeField]
        public Image OnBackgroundImage { get; private set; }

        [field: SerializeField]
        public Image OffBackgroundImage { get; private set; }
    }
}
