using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class MaxSimultaneousVideosSettingsController : SettingsFeatureController
    {
        private const string MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY = "Settings_MaxSimultaneousVideos";

        private readonly SettingsSliderModuleView view;

        public MaxSimultaneousVideosSettingsController(SettingsSliderModuleView view)
        {
            this.view = view;

            if (settingsDataStore.HasKey(MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetMaxSimultaneousVideosSettings);
            SetMaxSimultaneousVideosSettings(view.SliderView.Slider.value);
        }

        private void SetMaxSimultaneousVideosSettings(float maxVideos) =>
            settingsDataStore.SetSliderValue(MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY, maxVideos);

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetMaxSimultaneousVideosSettings);
        }
    }
}
