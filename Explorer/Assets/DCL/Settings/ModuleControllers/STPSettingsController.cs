using DCL.Settings.ModuleViews;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Settings.ModuleControllers
{
    public class STPSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private const string STP_DATA_STORE_KEY = "Settings_STP";

        public STPSettingsController(SettingsSliderModuleView viewInstance, ISettingsModuleEventListener settingsEventListener)
        {
            view = viewInstance;

            if (settingsDataStore.HasKey(STP_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(STP_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetSTPSetting);
            SetSTPSetting(view.SliderView.Slider.value);

            settingsEventListener.ResolutionChanged += ResolutionChanged;
        }

        private void ResolutionChanged(Resolution resolution)
        {
            var stpScale = 1f;

            if (resolution.width > 3000 || resolution.height > 3000)
                stpScale = 0.5f;
            else if (resolution.width > 2000 || resolution.height > 2000)
                stpScale = 0.6f;

            view.SliderView.Slider.value = stpScale;
        }

        private void SetSTPSetting(float sliderValue)
        {
            foreach (RenderPipelineAsset allConfiguredRenderPipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ((UniversalRenderPipelineAsset)allConfiguredRenderPipeline).renderScale = sliderValue;

            settingsDataStore.SetSliderValue(STP_DATA_STORE_KEY, sliderValue, save: true);
        }

        public override void Dispose() { }
    }
}
