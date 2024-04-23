using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class AvatarSoundsVolumeSettingsController : SettingsFeatureController
    {
        private const string AVATAR_VOLUME_EXPOSED_PARAM = "Avatar_Volume";
        private const string AVATAR_VOLUME_DATA_STORE_KEY = "Settings_AvatarVolume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public AvatarSoundsVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (settingsDataStore.HasKey(AVATAR_VOLUME_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(AVATAR_VOLUME_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetAvatarVolumeSettings);
            SetAvatarVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetAvatarVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(AVATAR_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            settingsDataStore.SetSliderValue(AVATAR_VOLUME_DATA_STORE_KEY, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetAvatarVolumeSettings);
        }
    }
}
