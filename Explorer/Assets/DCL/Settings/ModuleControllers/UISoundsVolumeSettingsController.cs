using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class UISoundsVolumeSettingsController : SettingsFeatureController
    {
        private const string UI_VOLUME_EXPOSED_PARAM = "UI_Volume";
        private const string UI_VOLUME_DATA_STORE_KEY = "Settings_UIVolume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public UISoundsVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (settingsDataStore.HasKey(UI_VOLUME_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(UI_VOLUME_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetUIVolumeSettings);
            SetUIVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetUIVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(UI_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            settingsDataStore.SetSliderValue(UI_VOLUME_DATA_STORE_KEY, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetUIVolumeSettings);
        }
    }
}
