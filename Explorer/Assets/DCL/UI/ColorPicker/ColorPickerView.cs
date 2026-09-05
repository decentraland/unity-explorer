using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ColorPickerView : ViewBase, IView
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

        /// <summary>
        /// Parent transform where color preset toggles are instantiated
        /// </summary>
        [field: SerializeField]
        public Transform ColorPresetsParent { get; private set; }

        [field: SerializeField]
        public GameObject ColorSelectorObject { get; private set; }

        [field: SerializeField]
        public RectTransform ColorControlsContainer { get; private set; }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (SliderHue != null)
                SliderHue.gameObject.SetActive(EnableHueSlider);

            if (SliderSaturation != null)
                SliderSaturation.gameObject.SetActive(EnableSaturationSlider);

            if (SliderValue != null)
                SliderValue.gameObject.SetActive(EnableValueSlider);
        }
#endif
    }
}
