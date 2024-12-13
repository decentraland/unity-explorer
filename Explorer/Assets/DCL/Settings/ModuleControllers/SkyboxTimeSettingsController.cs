using DCL.Settings.ModuleViews;
using DCL.StylizedSkybox.Scripts.Plugin;

namespace DCL.Settings.ModuleControllers
{
    public class SkyboxTimeSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly StylizedSkyboxSettingsAsset skyboxPlugin;

        public SkyboxTimeSettingsController(SettingsSliderModuleView view, StylizedSkyboxSettingsAsset skyboxPlugin)
        {
            this.view = view;
            this.skyboxPlugin = skyboxPlugin;

            view.SliderView.Slider.value = skyboxPlugin.TimeOfDay;
            view.SliderView.Slider.onValueChanged.AddListener(SetSkyboxTimeOfDay);
        }

        private void SetSkyboxTimeOfDay(float hour)
        {
           skyboxPlugin.TimeOfDay = (int)hour;
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetSkyboxTimeOfDay);
        }
    }
}
