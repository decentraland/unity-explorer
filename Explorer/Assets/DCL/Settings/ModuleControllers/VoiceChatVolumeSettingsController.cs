using DCL.FeatureFlags;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class VoiceChatVolumeSettingsController : SettingsFeatureController
    {
        private const string VOICE_CHAT_VOLUME_EXPOSED_PARAM = "VoiceChat_Volume";
        private const string VOICE_CHAT_VOLUME_DATA_STORE_KEY = "Settings_VoiceChatVolume";

        private readonly SettingsSliderModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public VoiceChatVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (settingsDataStore.HasKey(VOICE_CHAT_VOLUME_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(VOICE_CHAT_VOLUME_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetVoiceChatVolumeSettings);
            SetVoiceChatVolumeSettings(view.SliderView.Slider.value);

            view.SetActive(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.VOICE_CHAT));
        }

        private void SetVoiceChatVolumeSettings(float volumePercentage)
        {
            generalAudioMixer.SetFloat(VOICE_CHAT_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            settingsDataStore.SetSliderValue(VOICE_CHAT_VOLUME_DATA_STORE_KEY, volumePercentage, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetVoiceChatVolumeSettings);
        }
    }
}
