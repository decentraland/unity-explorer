using DCL.UI;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public enum SliderType
    {
        Numeric,
        Percentage,
        Time,
    }
    public class SettingsSliderModuleView : SettingsModuleView<SettingsSliderModuleView.Config>
    {
        private SliderType sliderType;

        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public SliderType sliderType;
            public float minValue;
            public float maxValue;
            public bool wholeNumbers;
        }

        [field: SerializeField] public SliderView SliderView { get; private set; }
        [field: SerializeField] public TMP_Text SliderValueText { get; private set; }

        private void Awake()
        {
            SliderView.Slider.onValueChanged.AddListener(OnValueChanged);
            OnValueChanged(SliderView.Slider.value);
        }

        protected override void Configure(Config configuration)
        {
            SliderView.Slider.interactable = configuration.IsEnabled;
            SliderView.Slider.minValue = configuration.minValue;
            SliderView.Slider.maxValue = configuration.maxValue;
            SliderView.Slider.wholeNumbers = configuration.wholeNumbers;
            sliderType = configuration.sliderType;
        }

        private void OnValueChanged(float value)
        {
            switch (sliderType)
            {
                case SliderType.Numeric:
                    SliderValueText.text = value.ToString(SliderView.Slider.wholeNumbers ? "0" : "0.00", CultureInfo.InvariantCulture);
                    break;
                case SliderType.Percentage:
                    SliderValueText.text = $"{value.ToString(SliderView.Slider.wholeNumbers ? "0" : "0.00", CultureInfo.InvariantCulture)}%";
                    break;
                case SliderType.Time:
                    value = Mathf.Clamp(value, 0, 23.998f);
                    var hourSection = (int)value;
                    var minuteSection = (int)((value - hourSection) * 60);
                    SliderValueText.text = $"{hourSection:00}:{minuteSection:00}";
                    break;
            }
        }
    }
}
