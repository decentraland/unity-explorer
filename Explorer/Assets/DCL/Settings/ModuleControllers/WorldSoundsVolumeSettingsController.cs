using DCL.Audio;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class WorldSoundsVolumeSettingsController : SettingsFeatureController
    {
        private const string WORLD_VOLUME_EXPOSED_PARAM = "World_Volume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;
        private readonly VolumeBus volumeBus;

        public WorldSoundsVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, VolumeBus volumeBus)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;
            this.volumeBus = volumeBus;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_WORLD_VOLUME))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_WORLD_VOLUME);

            view.SliderView.Slider.onValueChanged.AddListener(SetWorldVolumeSettings);
            SetWorldVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetWorldVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(WORLD_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_WORLD_VOLUME, volumePercentage, save: true);

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetWorldVolume(volumePercentage / 100);
#endif
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetWorldVolumeSettings);
        }
    }
}
