using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class ChatBubblesVisibilityController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly ISettingsModuleEventListener settingsEventListener;

        public ChatBubblesVisibilityController(SettingsDropdownModuleView view, ChatSettingsAsset chatSettingsAsset, ISettingsModuleEventListener settingsEventListener)
        {
            this.view = view;
            this.chatSettingsAsset = chatSettingsAsset;
            this.settingsEventListener = settingsEventListener;

            if (SettingsDataStore.HasKey(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY))
                view.DropdownView.Dropdown.value = SettingsDataStore.GetDropdownValue(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetSettings);
        }

        private void SetSettings(int index)
        {
            switch (index)
            {
                case (int)ChatBubbleVisibilitySettings.ALL:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.ALL);
                    settingsEventListener.NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings.ALL);
                    break;
                case (int)ChatBubbleVisibilitySettings.NEARBY_ONLY:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.NEARBY_ONLY);
                    settingsEventListener.NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings.NEARBY_ONLY);
                    break;
                case (int)ChatBubbleVisibilitySettings.NONE:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.NONE);
                    settingsEventListener.NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings.NONE);
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatPrivacySettingsController: {index}");
                    return;
            }

            SettingsDataStore.SetDropdownValue(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSettings);
        }
    }
}
