using DCL.Audio;
using DCL.FeatureFlags;
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
        private readonly VolumeBus volumeBus;

        public VoiceChatVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, VolumeBus volumeBus)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;
            this.volumeBus = volumeBus;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_VOICE_CHAT_VOLUME))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_VOICE_CHAT_VOLUME);

            view.SliderView.Slider.onValueChanged.AddListener(SetVoiceChatVolumeSettings);
            SetVoiceChatVolumeSettings(view.SliderView.Slider.value);

            volumeBus.OnVoiceChatVolumeChanged += OnVoiceChatVolumeChangedExternally;

            bool isVoiceChatEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT);
            view.SetActive(isVoiceChatEnabled);
        }

        private void SetVoiceChatVolumeSettings(float volumePercentage)
        {
            float db = AudioUtils.PercentageVolumeToDecibel(volumePercentage);
            generalAudioMixer.SetFloat(VOICE_CHAT_VOLUME_EXPOSED_PARAM, db);
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_VOICE_CHAT_VOLUME, volumePercentage, save: true);
            volumeBus.SetVoiceChatVolume(volumePercentage);
        }

        private void OnVoiceChatVolumeChangedExternally(float volumePercentage)
        {
            view.ConfigureWithoutNotify(volumePercentage);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetVoiceChatVolumeSettings);
            volumeBus.OnVoiceChatVolumeChanged -= OnVoiceChatVolumeChangedExternally;
        }
    }
}
