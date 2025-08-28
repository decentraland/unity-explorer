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

            if (DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MUSIC_AND_SFX_MUTED))
            {
                view.SliderView.Slider.value = 0;
                SetWorldVolumeSettingsWithoutSerialization(0);
            }
            else if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_WORLD_VOLUME))
            {
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_WORLD_VOLUME);
                SetWorldVolumeSettings(view.SliderView.Slider.value);
            }
            else
            {
                view.SliderView.Slider.value = 1;
                SetWorldVolumeSettings(1);
            }

            view.SliderView.Slider.onValueChanged.AddListener(SetWorldVolumeSettings);
            volumeBus.OnMusicAndSFXMuteChanged += SettingsMuteChanged;
        }

        private void SetWorldVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(WORLD_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_WORLD_VOLUME, volumePercentage, save: true);

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetWorldVolume(volumePercentage / 100);
#endif
        }
        
        private void SetWorldVolumeSettingsWithoutSerialization(float volumePercentage)
        {
            generalAudioMixer.SetFloat(WORLD_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMusicVolume(volumePercentage / 100);
#endif
        }

        private void SettingsMuteChanged(bool value)
        {
            float volumePercentage = 0;
            if (value || !DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_WORLD_VOLUME))
                view.SliderView.Slider.SetValueWithoutNotify(0);
            else
            {
                volumePercentage = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_WORLD_VOLUME);
                view.SliderView.Slider.SetValueWithoutNotify(volumePercentage);
            }
            
            generalAudioMixer.SetFloat(WORLD_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMusicVolume(volumePercentage / 100);
#endif
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetWorldVolumeSettings);
            volumeBus.OnMusicAndSFXMuteChanged -= SettingsMuteChanged;
        }
    }
}
