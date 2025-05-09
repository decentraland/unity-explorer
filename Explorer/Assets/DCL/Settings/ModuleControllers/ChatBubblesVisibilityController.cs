using DCL.Diagnostics;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class ChatBubblesVisibilityController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly ISettingsModuleEventListener settingsEventListener;
        private const string CHAT_BUBBLES_VISIBILITY_SETTINGS_STORE_KEY = "Settings_GraphicsQuality";

        public ChatBubblesVisibilityController(SettingsDropdownModuleView view, ChatSettingsAsset chatSettingsAsset, ISettingsModuleEventListener settingsEventListener)
        {
            this.view = view;
            this.chatSettingsAsset = chatSettingsAsset;
            this.settingsEventListener = settingsEventListener;

            if(settingsDataStore.HasKey(CHAT_BUBBLES_VISIBILITY_SETTINGS_STORE_KEY))
                view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(CHAT_BUBBLES_VISIBILITY_SETTINGS_STORE_KEY);

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

            settingsDataStore.SetDropdownValue(CHAT_BUBBLES_VISIBILITY_SETTINGS_STORE_KEY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSettings);
        }
    }
}
