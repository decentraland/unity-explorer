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
        private const float MAX_TIME_VALUE = 23.998f;

        private SliderType sliderType;

        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public SliderType sliderType;
            public float minValue;
            public float maxValue;
            public bool wholeNumbers;
            public float defaultValue;
        }

        [field: SerializeField] public SliderView SliderView { get; private set; }
        [field: SerializeField] public TMP_Text SliderValueText { get; private set; }

        private void Awake()
        {
            SliderView.Slider.onValueChanged.AddListener(OnSliderValueChanged);
            SliderView.DecreaseButton.onClick.AddListener(OnDecreaseButtonClicked);
            SliderView.IncreaseButton.onClick.AddListener(OnIncreaseButtonClicked);
            OnSliderValueChanged(SliderView.Slider.value);
        }

        protected override void Configure(Config configuration)
        {
            SliderView.DecreaseButton.gameObject.SetActive(configuration.IsEnabled);
            SliderView.IncreaseButton.gameObject.SetActive(configuration.IsEnabled);
            SliderView.Slider.interactable = configuration.IsEnabled;
            SliderView.Slider.minValue = configuration.minValue;
            SliderView.Slider.maxValue = configuration.maxValue;
            SliderView.Slider.wholeNumbers = configuration.wholeNumbers;
            SliderView.Slider.value = configuration.defaultValue;
            sliderType = configuration.sliderType;
        }

        private void OnSliderValueChanged(float value)
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
                    value = Mathf.Clamp(value, 0, MAX_TIME_VALUE);
                    var hourSection = (int)value;
                    var minuteSection = (int)((value - hourSection) * 60);
                    SliderValueText.text = $"{hourSection:00}:{minuteSection:00}";
                    break;
            }

            SliderView.DecreaseButton.interactable = value > SliderView.Slider.minValue;
            SliderView.IncreaseButton.interactable = value < (sliderType == SliderType.Time ? Mathf.Clamp(SliderView.Slider.maxValue, 0, MAX_TIME_VALUE) : SliderView.Slider.maxValue);
        }

        private void OnDecreaseButtonClicked()
        {
            if (!SliderView.Slider.interactable)
                return;

            SliderView.Slider.value -= 1;
        }

        private void OnIncreaseButtonClicked()
        {
            if (!SliderView.Slider.interactable)
                return;

            SliderView.Slider.value += 1;
        }
    }
}
