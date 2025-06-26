using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class AvatarSoundsVolumeSettingsController : SettingsFeatureController
    {
        private const string AVATAR_VOLUME_EXPOSED_PARAM = "Avatar_Volume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public AvatarSoundsVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_AVATAR_VOLUME))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetSliderValue(DCLPrefKeys.SETTINGS_AVATAR_VOLUME);

            view.SliderView.Slider.onValueChanged.AddListener(SetAvatarVolumeSettings);
            SetAvatarVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetAvatarVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(AVATAR_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_AVATAR_VOLUME, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetAvatarVolumeSettings);
        }
    }
}
