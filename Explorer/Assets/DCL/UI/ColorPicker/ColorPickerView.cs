using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ColorPickerView : MonoBehaviour
    {
        [field: Header("Hue Slider")]
        [field: SerializeField]
        public SliderView SliderHue { get; private set; }

        [field: SerializeField]
        public bool EnableHueSlider { get; set; } = true;

        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float DefaultHue { get; private set; } = 1f;

        [field: Header("Saturation Slider")]
        [field: SerializeField]
        public SliderView SliderSaturation { get; private set; }

        [field: SerializeField]
        public bool EnableSaturationSlider { get; set; } = true;

        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float DefaultSaturation { get; private set; } = 1f;

        [field: Header("Value Slider")]
        [field: SerializeField]
        public SliderView SliderValue { get; private set; }

        [field: SerializeField]
        public bool EnableValueSlider { get; set; } = true;

        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float DefaultValue { get; private set; } = 1f;

        [field: Header("Toggle Button")]
        [field: SerializeField]
        public Button ToggleButton { get; private set; }

        /// <summary>
        /// The "COLOR" text GameObject child under ToggleButton
        /// </summary>
        [field: SerializeField]
        public GameObject ToggleText { get; private set; }

        [field: SerializeField]
        public bool ShowToggleText { get; set; } = true;

        [field: Header("Container")]
        [field: SerializeField]
        public GameObject Container { get; private set; }

        [field: SerializeField]
        public Image ContainerImage { get; private set; }

        [field: Header("Color Display")]
        [field: SerializeField]
        public Image ColorPreviewImage { get; private set; }

        /// <summary>
        /// Parent transform where color preset toggles are instantiated
        /// </summary>
        [field: SerializeField]
        public Transform ColorPresetsParent { get; private set; }

        [field: SerializeField]
        public GameObject ColorSelectorObject { get; private set; }

        [field: Header("UI Indicators")]
        [field: SerializeField]
        public GameObject ArrowUpMark { get; private set; }

        [field: SerializeField]
        public GameObject ArrowDownMark { get; private set; }
    }
}
