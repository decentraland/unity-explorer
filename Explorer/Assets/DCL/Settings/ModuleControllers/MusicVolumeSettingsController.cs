using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class MusicVolumeSettingsController : SettingsFeatureController
    {
        private const string MUSIC_VOLUME_EXPOSED_PARAM = "Music_Volume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public MusicVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (SettingsDataStore.HasKey(DCLPrefKeys.SETTINGS_MUSIC_VOLUME))
                view.SliderView.Slider.value = SettingsDataStore.GetSliderValue(DCLPrefKeys.SETTINGS_MUSIC_VOLUME);

            view.SliderView.Slider.onValueChanged.AddListener(SetMusicVolumeSettings);
            SetMusicVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetMusicVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(MUSIC_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            SettingsDataStore.SetSliderValue(DCLPrefKeys.SETTINGS_MUSIC_VOLUME, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetMusicVolumeSettings);
        }
    }
}
