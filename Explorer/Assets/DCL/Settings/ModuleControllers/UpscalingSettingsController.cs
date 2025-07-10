using DCL.Settings.ModuleViews;
using DCL.Utilities;
using System;

namespace DCL.Settings.ModuleControllers
{
    public class UpscalingSettingsController : SettingsFeatureController
    {
        //This is a special case slider. To be able to step values, the min and max alue are 5 and 12, respectively.
        //Then again, the value to set up the UpscalerController is in decimals, 0.5 to 1.2
        private const float STEP_MULTIPLIER = 10f;

        private readonly SettingsSliderModuleView viewInstance;
        private readonly UpscalingController upscalingController;

        public UpscalingSettingsController(SettingsSliderModuleView viewInstance, UpscalingController upscalingController)
        {
            this.viewInstance = viewInstance;
            this.upscalingController = upscalingController;
            viewInstance.SliderView.Slider.onValueChanged.AddListener(UpdateUpscalingValue);

            viewInstance.SliderView.Slider.SetValueWithoutNotify(upscalingController.GetCurrentUpscale() * STEP_MULTIPLIER);
            UpdateUpscalingValue(upscalingController.GetCurrentUpscale() * STEP_MULTIPLIER);
        }

        private void UpdateUpscalingValue(float value)
        {
            //Sent in decimal form
            upscalingController.UpdateUpscaling(value / STEP_MULTIPLIER);
            viewInstance.RevaluateButtonLimits(value);
            //Display is in the hundreds
            viewInstance.SliderValueText.text = $"{value * STEP_MULTIPLIER}%";
        }


        public override void Dispose()
        {
            viewInstance.SliderView.Slider.onValueChanged.RemoveListener(UpdateUpscalingValue);
        }
    }
}
