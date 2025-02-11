using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class ChatSoundsSettingsController : SettingsFeatureController
    {
        private const string CHAT_VOLUME_EXPOSED_PARAM = "Chat_Volume";
        private const string CHAT_SOUNDS_DATA_STORE_KEY = "Settings_ChatSounds";

        private readonly SettingsDropdownModuleView view;
        private readonly ChatAudioSettingsAsset chatAudioSettingsAsset;
        private readonly AudioMixer generalAudioMixer;

        public ChatSoundsSettingsController(SettingsDropdownModuleView view, AudioMixer generalAudioMixer, ChatAudioSettingsAsset chatAudioSettingsAsset)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;
            this.chatAudioSettingsAsset = chatAudioSettingsAsset;

            if (settingsDataStore.HasKey(CHAT_SOUNDS_DATA_STORE_KEY))
                view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(CHAT_SOUNDS_DATA_STORE_KEY);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetChatSoundsSettings);
        }

        private void SetChatSoundsSettings(int index)
        {
            if (index == 2)
            {
                chatAudioSettingsAsset.chatSettings = ChatSettings.None;
                generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(0f));
            }
            else
            {
                generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(100f));
                chatAudioSettingsAsset.chatSettings = index == 1 ? ChatSettings.Mentions : ChatSettings.All;
            }

            settingsDataStore.SetDropdownValue(CHAT_SOUNDS_DATA_STORE_KEY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetChatSoundsSettings);
        }
    }
}
