using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class MusicVolumeSettingsController : SettingsFeatureController
    {
        private const string MUSIC_VOLUME_EXPOSED_PARAM = "Music_Volume";
        private const string MUSIC_VOLUME_DATA_STORE_KEY = "Settings_MusicVolume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public MusicVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (settingsDataStore.HasKey(MUSIC_VOLUME_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(MUSIC_VOLUME_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetMusicVolumeSettings);
            SetMusicVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetMusicVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(MUSIC_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            settingsDataStore.SetSliderValue(MUSIC_VOLUME_DATA_STORE_KEY, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetMusicVolumeSettings);
        }
    }
}
