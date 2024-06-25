using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ColorPickerView : MonoBehaviour
    {
        [field: SerializeField]
        public SliderView SliderHue { get; private set; }

        [field: SerializeField]
        public SliderView SliderSaturation { get; private set; }

        [field: SerializeField]
        public SliderView SliderValue { get; private set; }

        [field: SerializeField]
        public Button ToggleButton { get; private set; }

        [field: SerializeField]
        public GameObject Container { get; private set; }

        [field: SerializeField]
        public Image ContainerImage { get; private set; }

        [field: SerializeField]
        public Transform ColorPresetsParent { get; private set; }

        [field: SerializeField]
        public GameObject ColorSelectorObject { get; private set; }

        [field: SerializeField]
        public Image ColorPreviewImage { get; private set; }

        [field: SerializeField]
        public GameObject ArrowUpMark { get; private set; }

        [field: SerializeField]
        public GameObject ArrowDownMark { get; private set; }
    }
}
