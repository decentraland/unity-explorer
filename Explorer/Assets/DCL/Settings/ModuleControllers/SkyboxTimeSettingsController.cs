using DCL.Settings.ModuleViews;
using DCL.StylizedSkybox.Scripts;

namespace DCL.Settings.ModuleControllers
{
    public class SkyboxTimeSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly StylizedSkyboxSettingsAsset skyboxSettingsAsset;

        public SkyboxTimeSettingsController(SettingsSliderModuleView view, StylizedSkyboxSettingsAsset skyboxSettingsAsset)
        {
            this.view = view;
            this.skyboxSettingsAsset = skyboxSettingsAsset;

            view.SliderView.Slider.value = skyboxSettingsAsset.TimeOfDay;
            view.SliderView.Slider.onValueChanged.AddListener(SetSkyboxTimeOfDay);
        }

        private void SetSkyboxTimeOfDay(float hour)
        {
           skyboxSettingsAsset.TimeOfDay = (int)hour;
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetSkyboxTimeOfDay);
        }
    }
}
