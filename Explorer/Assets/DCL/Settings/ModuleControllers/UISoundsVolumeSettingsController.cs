using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class UISoundsVolumeSettingsController : SettingsFeatureController
    {
        private const string UI_VOLUME_EXPOSED_PARAM = "UI_Volume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public UISoundsVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_UI_VOLUME))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_UI_VOLUME);

            view.SliderView.Slider.onValueChanged.AddListener(SetUIVolumeSettings);
            SetUIVolumeSettings(view.SliderView.Slider.value);
        }

        private void SetUIVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(UI_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_UI_VOLUME, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetUIVolumeSettings);
        }
    }
}
