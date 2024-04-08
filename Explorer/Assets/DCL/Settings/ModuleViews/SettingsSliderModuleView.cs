using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class SettingsSliderModuleView : SettingsModuleView<SettingsSliderModuleView.Config>
    {
        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public float minValue;
            public float maxValue;
            public bool wholeNumbers;
        }

        [field: SerializeField] public SliderView SliderView { get; private set; }

        protected override void Configure(Config configuration)
        {
            SliderView.Slider.interactable = configuration.IsEnabled;
            SliderView.Slider.minValue = configuration.minValue;
            SliderView.Slider.maxValue = configuration.maxValue;
            SliderView.Slider.wholeNumbers = configuration.wholeNumbers;
        }
    }
}
