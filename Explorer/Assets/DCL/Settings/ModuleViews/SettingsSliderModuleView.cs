using System;
using UnityEngine;
using UnityEngine.UI;

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

        [field: SerializeField] public Slider Slider { get; private set; }

        protected override void Configure(Config configuration)
        {
            Slider.interactable = configuration.IsEnabled;
            Slider.minValue = configuration.minValue;
            Slider.maxValue = configuration.maxValue;
            Slider.wholeNumbers = configuration.wholeNumbers;
        }
    }
}
