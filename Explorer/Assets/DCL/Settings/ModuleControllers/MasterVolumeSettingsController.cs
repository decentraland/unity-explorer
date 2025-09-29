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

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MASTER_VOLUME))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_MASTER_VOLUME);

            view.SliderView.Slider.onValueChanged.AddListener(SetMasterVolumeSettings);
            SetMasterVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetMasterVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(MASTER_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_MASTER_VOLUME, volumePercentage, save: true);
            volumeBus.SetMasterVolume(volumePercentage / 100);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetMasterVolumeSettings);
        }
    }
}
