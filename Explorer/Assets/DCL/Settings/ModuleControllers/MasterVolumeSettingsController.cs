using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class MasterVolumeSettingsController : SettingsFeatureController
    {
        private const string MASTER_VOLUME_EXPOSED_PARAM = "Master_Volume";
        private const string MASTER_VOLUME_DATA_STORE_KEY = "Settings_MasterVolume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;
        private readonly WorldVolumeMacBus worldVolumeMacBus;

        public MasterVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, WorldVolumeMacBus worldVolumeMacBus)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;
            this.worldVolumeMacBus = worldVolumeMacBus;

            if (settingsDataStore.HasKey(MASTER_VOLUME_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(MASTER_VOLUME_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetMasterVolumeSettings);
            SetMasterVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetMasterVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(MASTER_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            settingsDataStore.SetSliderValue(MASTER_VOLUME_DATA_STORE_KEY, volumePercentage, save: true);

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            worldVolumeMacBus.SetMasterVolume(volumePercentage / 100);
#endif
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetMasterVolumeSettings);
        }
    }
}
