using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class ChatSoundsSettingsController : SettingsFeatureController
    {
        private const string CHAT_VOLUME_EXPOSED_PARAM = "Chat_Volume";
        private const string CHAT_SOUNDS_DATA_STORE_KEY = "Settings_ChatSounds";

        private readonly SettingsToggleModuleView view;
        private readonly AudioMixer generalAudioMixer;

        public ChatSoundsSettingsController(SettingsToggleModuleView view, AudioMixer generalAudioMixer)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;

            if (settingsDataStore.HasKey(CHAT_SOUNDS_DATA_STORE_KEY))
                view.ToggleView.Toggle.isOn = settingsDataStore.GetToggleValue(CHAT_SOUNDS_DATA_STORE_KEY);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetChatSoundsSettings);
            SetChatSoundsSettings(view.ToggleView.Toggle.isOn);
        }

        private void SetChatSoundsSettings(bool isOn)
        {
            generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(isOn ? 100f : 0f));
            settingsDataStore.SetToggleValue(CHAT_SOUNDS_DATA_STORE_KEY, isOn, save: true);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveListener(SetChatSoundsSettings);
        }
    }
}
