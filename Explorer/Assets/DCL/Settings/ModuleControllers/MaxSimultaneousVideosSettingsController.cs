using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleViews;

namespace DCL.Settings.ModuleControllers
{
    public class MaxSimultaneousVideosSettingsController : SettingsFeatureController
    {
        private const string MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY = "Settings_MaxSimultaneousVideos";

        private readonly SettingsSliderModuleView view;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;

        public MaxSimultaneousVideosSettingsController(SettingsSliderModuleView view, VideoPrioritizationSettings videoPrioritizationSettings)
        {
            this.view = view;
            this.videoPrioritizationSettings = videoPrioritizationSettings;

            if (settingsDataStore.HasKey(MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetMaxSimultaneousVideosSettings);
            SetMaxSimultaneousVideosSettings(view.SliderView.Slider.value);

            videoPrioritizationSettings.MaximumSimultaneousVideosChanged += OnMaximumSimultaneousVideosChanged;
        }

        private void SetMaxSimultaneousVideosSettings(float maxVideos) =>
            videoPrioritizationSettings.MaximumSimultaneousVideos = (int)maxVideos;

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetMaxSimultaneousVideosSettings);
            videoPrioritizationSettings.MaximumSimultaneousVideosChanged -= OnMaximumSimultaneousVideosChanged;
        }

        private void OnMaximumSimultaneousVideosChanged(int newValue)
        {
            view.SliderView.Slider.value = newValue;
            settingsDataStore.SetSliderValue(MAX_SIMULTANEOUS_VIDEOS_DATA_STORE_KEY, newValue, save: true);
        }
    }
}
