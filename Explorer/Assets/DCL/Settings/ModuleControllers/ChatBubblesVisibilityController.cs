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

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetSettings);
        }

        private void SetSettings(int index)
        {
            switch (index)
            {
                case (int)ChatBubbleVisibilitySettings.All:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.All);
                    settingsEventListener.NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings.All);
                    break;
                case (int)ChatBubbleVisibilitySettings.NearbyOnly:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.NearbyOnly);
                    settingsEventListener.NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings.NearbyOnly);
                    break;
                case (int)ChatBubbleVisibilitySettings.None:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.None);
                    settingsEventListener.NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings.None);
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatPrivacySettingsController: {index}");
                    return;
            }

            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSettings);
        }
    }
}
