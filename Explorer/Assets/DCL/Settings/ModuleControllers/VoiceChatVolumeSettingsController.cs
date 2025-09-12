using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;
using DCL.Prefs;

namespace DCL.Settings.ModuleControllers
{
    public class VoiceChatVolumeSettingsController : SettingsFeatureController
    {
        private const string VOICE_CHAT_VOLUME_EXPOSED_PARAM = "VoiceChat_Volume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public VoiceChatVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, bool isVoiceChatEnabled)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_VOICE_CHAT_VOLUME))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_VOICE_CHAT_VOLUME);

            view.SliderView.Slider.onValueChanged.AddListener(SetVoiceChatVolumeSettings);
            SetVoiceChatVolumeSettings(view.SliderView.Slider.value);

            view.SetActive(isVoiceChatEnabled);
        }

        private void SetVoiceChatVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(VOICE_CHAT_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_VOICE_CHAT_VOLUME, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetVoiceChatVolumeSettings);
        }
    }
}
