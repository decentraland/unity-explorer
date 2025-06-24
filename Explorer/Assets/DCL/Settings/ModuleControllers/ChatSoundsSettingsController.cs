using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
    public class ChatSoundsSettingsController : SettingsFeatureController
    {
        private const string CHAT_VOLUME_EXPOSED_PARAM = "Chat_Volume";

        private readonly SettingsDropdownModuleView view;
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly AudioMixer generalAudioMixer;

        public ChatSoundsSettingsController(SettingsDropdownModuleView view, AudioMixer generalAudioMixer, ChatSettingsAsset chatSettingsAsset)
        {
            this.view = view;
            this.generalAudioMixer = generalAudioMixer;
            this.chatSettingsAsset = chatSettingsAsset;

            if (SettingsDataStore.HasKey(DCLPrefKeys.SETTINGS_CHAT_SOUNDS))
                view.DropdownView.Dropdown.value = SettingsDataStore.GetDropdownValue(DCLPrefKeys.SETTINGS_CHAT_SOUNDS);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetChatSoundsSettings);
        }

        private void SetChatSoundsSettings(int index)
        {
            switch (index)
            {
                case (int)ChatAudioSettings.NONE:
                    chatSettingsAsset.chatAudioSettings = ChatAudioSettings.NONE;
                    generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(0f));
                    break;
                case (int)ChatAudioSettings.MENTIONS_ONLY:
                    chatSettingsAsset.chatAudioSettings = ChatAudioSettings.MENTIONS_ONLY;
                    generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(100f));
                    break;
                case (int)ChatAudioSettings.ALL:
                    chatSettingsAsset.chatAudioSettings = ChatAudioSettings.ALL;
                    generalAudioMixer.SetFloat(CHAT_VOLUME_EXPOSED_PARAM, AudioUtils.PercentageVolumeToDecibel(100f));
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatSoundsSettingsController: {index}");
                    return;
            }

            SettingsDataStore.SetDropdownValue(DCLPrefKeys.SETTINGS_CHAT_SOUNDS, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetChatSoundsSettings);
        }
    }
}
