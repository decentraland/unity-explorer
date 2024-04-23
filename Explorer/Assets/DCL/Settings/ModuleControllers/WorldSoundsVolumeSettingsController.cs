using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class WorldSoundsVolumeSettingsController : SettingsFeatureController
    {
        private const string WORLD_VOLUME_EXPOSED_PARAM = "World_Volume";
        private const string WORLD_VOLUME_DATA_STORE_KEY = "Settings_WorldVolume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public WorldSoundsVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (settingsDataStore.HasKey(WORLD_VOLUME_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(WORLD_VOLUME_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetWorldVolumeSettings);
            SetWorldVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetWorldVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(WORLD_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            settingsDataStore.SetSliderValue(WORLD_VOLUME_DATA_STORE_KEY, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetWorldVolumeSettings);
        }
    }
}
