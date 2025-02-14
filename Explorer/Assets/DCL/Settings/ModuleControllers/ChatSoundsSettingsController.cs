using DCL.Diagnostics;
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
            switch (index)
            {
                case (int)ChatAudioSettings.NONE:
                    chatAudioSettingsAsset.chatAudioSettings = ChatAudioSettings.NONE;
                    generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(0f));
                    break;
                case (int)ChatAudioSettings.MENTIONS_ONLY:
                    chatAudioSettingsAsset.chatAudioSettings = ChatAudioSettings.MENTIONS_ONLY;
                    generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(100f));
                    break;
                case (int)ChatAudioSettings.ALL:
                    chatAudioSettingsAsset.chatAudioSettings = ChatAudioSettings.ALL;
                    generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(100f));
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatSoundsSettingsController: {index}");
                    return;
            }

            settingsDataStore.SetDropdownValue(CHAT_SOUNDS_DATA_STORE_KEY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetChatSoundsSettings);
        }
    }
}
