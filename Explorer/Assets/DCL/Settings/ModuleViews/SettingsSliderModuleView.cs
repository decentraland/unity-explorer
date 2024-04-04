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
        }

        [field: SerializeField] public Slider Slider { get; private set; }

        protected override void Configure(Config configuration)
        {
            Slider.minValue = configuration.minValue;
            Slider.maxValue = configuration.maxValue;
        }
    }
}
