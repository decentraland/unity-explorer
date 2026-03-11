using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using System;

namespace DCL.Settings.ModuleControllers
{
    public class UpscalingSettingsController : SettingsFeatureController
    {
        //This is a special case slider. To be able to step values, the min and max alue are 5 and 12, respectively.
        //Then again, the value to set up the UpscalerController is in decimals, 0.5 to 1.2
        private const float STEP_MULTIPLIER = 10f;

        private readonly SettingsSliderModuleView viewInstance;
        private readonly IQualitySettingsController qualitySettingsController;

        public UpscalingSettingsController(SettingsSliderModuleView viewInstance, IQualitySettingsController qualitySettingsController)
        {
            this.viewInstance = viewInstance;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            viewInstance.SliderView.Slider.onValueChanged.AddListener(UpdateUpscalingValue);

            viewInstance.ConfigureWithoutNotify(qualitySettingsController.ResolutionScale * STEP_MULTIPLIER);
            UpdateVisuals(qualitySettingsController.ResolutionScale * STEP_MULTIPLIER);
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            viewInstance.ConfigureWithoutNotify(qualitySettingsController.ResolutionScale * STEP_MULTIPLIER);
            UpdateVisuals(qualitySettingsController.ResolutionScale * STEP_MULTIPLIER);
        }

        private void UpdateUpscalingValue(float value)
        {
            //Sent in decimal form
            qualitySettingsController.SetResolutionScale(value / STEP_MULTIPLIER);
            UpdateVisuals(value);
        }

        private void UpdateVisuals(float value)
        {
            viewInstance.RevaluateButtonLimits(value);
            //Display is in the hundreds
            viewInstance.SliderValueText.text = $"{value * STEP_MULTIPLIER}%";
        }


        public override void Dispose()
        {
            viewInstance.SliderView.Slider.onValueChanged.RemoveListener(UpdateUpscalingValue);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
