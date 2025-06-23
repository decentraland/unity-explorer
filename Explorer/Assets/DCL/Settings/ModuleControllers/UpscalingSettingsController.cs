using DCL.Settings.ModuleViews;
using DCL.Utilities;
using System;

namespace DCL.Settings.ModuleControllers
{
    public class UpscalingSettingsController : SettingsFeatureController
    {
        private const float STEP_MULTIPLIER = 10f;
        private readonly SettingsSliderModuleView viewInstance;
        private readonly UpscalingController upscalingController;

        public UpscalingSettingsController(SettingsSliderModuleView viewInstance, UpscalingController upscalingController)
        {
            this.viewInstance = viewInstance;
            this.upscalingController = upscalingController;
            upscalingController.OnUpscalingChanged += UpdateSliderText;
            viewInstance.SliderView.Slider.onValueChanged.AddListener(UpdateUpscalingValue);
        }

        private void UpdateUpscalingValue(float value)
        {
            upscalingController.SetSTPSetting(value / STEP_MULTIPLIER, true);
        }

        private void UpdateSliderText(float value)
        {
            viewInstance.SliderValueText.text = $"{value * STEP_MULTIPLIER}%";
        }

        public override void Dispose()
        {
        }
    }
}
