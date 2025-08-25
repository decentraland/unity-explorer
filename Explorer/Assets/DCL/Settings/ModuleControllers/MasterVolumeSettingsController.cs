using DCL.Audio;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class MasterVolumeSettingsController : SettingsFeatureController
    {
        private const string MASTER_VOLUME_EXPOSED_PARAM = "Master_Volume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;
        private readonly VolumeBus volumeBus;

        public MasterVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, VolumeBus volumeBus)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;
            this.volumeBus = volumeBus;

            if (DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_VOLUME_MUTED))
            {
                view.SliderView.Slider.value = 0;
                SetMasterVolumeSettingsWithoutSerialization(0);
            }
            else if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MASTER_VOLUME))
            {
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_MASTER_VOLUME);
                SetMasterVolumeSettings(view.SliderView.Slider.value);
            }
            
            view.SliderView.Slider.onValueChanged.AddListener(SetMasterVolumeSettings);
            volumeBus.OnGlobalMuteChanged += GlobalMuteChanged;
        }

        private void SetMasterVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(MASTER_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_MASTER_VOLUME, volumePercentage, save: true);

            if (volumePercentage > 0)
                volumeBus.SetGlobalMute(false);

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMasterVolume(volumePercentage / 100);
#endif
        }
        
        private void SetMasterVolumeSettingsWithoutSerialization(float volumePercentage)
        {
            generalAudioMixer.SetFloat(MASTER_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));

            if (volumePercentage > 0)
                volumeBus.SetGlobalMute(false);

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMasterVolume(volumePercentage / 100);
#endif
        }

        private void GlobalMuteChanged(bool value)
        {
            float volumePercentage = 0;
            if (value || !DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MASTER_VOLUME))
                view.SliderView.Slider.SetValueWithoutNotify(0);
            else
            {
                volumePercentage = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_MASTER_VOLUME);
                view.SliderView.Slider.SetValueWithoutNotify(volumePercentage);
            }
            
            generalAudioMixer.SetFloat(MASTER_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMasterVolume(volumePercentage / 100);
#endif
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetMasterVolumeSettings);
        }
    }
}
