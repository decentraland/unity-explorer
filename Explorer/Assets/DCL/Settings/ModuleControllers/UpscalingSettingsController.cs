using DCL.Settings.ModuleViews;
using DCL.Utilities;
using System;

namespace DCL.Settings.ModuleControllers
{
    public class UpscalingSettingsController : SettingsFeatureController
    {
        //This is a special case slider. To be able to step values, the min and max alue are 5 and 12, respectively.
        //Then again, the value to set up the UpscalerController is in decimals, 0.5 to 1.2
        //Finally, the text should be set that comes from the UpscalingController comes in decimals and should be shown between 50% and 120%.
        private const float STEP_MULTIPLIER = 10f;
        private const float STEP_MULTIPLIER_DISPLAY = 100f;

        private readonly SettingsSliderModuleView viewInstance;
        private readonly UpscalingController upscalingController;

        public UpscalingSettingsController(SettingsSliderModuleView viewInstance, UpscalingController upscalingController)
        {
            this.viewInstance = viewInstance;
            this.upscalingController = upscalingController;
            upscalingController.OnUpscalingChanged += UpdateSliderText;
            viewInstance.SliderView.Slider.onValueChanged.AddListener(UpdateUpscalingValue);

            UpdateSliderText(upscalingController.GetCurrentUpscale());
        }

        private void UpdateUpscalingValue(float value)
        {
            //Sent in decimal form
            upscalingController.SetUpscalingValue(value / STEP_MULTIPLIER, true);
        }

        private void UpdateSliderText(float value)
        {
            //Comes in decimal form
            viewInstance.SliderValueText.text = $"{value * STEP_MULTIPLIER_DISPLAY}%";
        }

        public override void Dispose()
        {
        }
    }
}
