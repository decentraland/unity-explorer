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
            {
                int storedIndex = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY);
                view.DropdownView.Dropdown.SetValueWithoutNotify(storedIndex);
                TrySetBubblesVisibility(storedIndex);
            }

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetSettings);
        }

        private void SetSettings(int index)
        {
            if (!TrySetBubblesVisibility(index))
                return;

            settingsEventListener.NotifyChatBubblesVisibilityChanged(chatSettingsAsset.chatBubblesVisibilitySettings);
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_CHAT_BUBBLES_VISIBILITY, index, save: true);
        }

        private bool TrySetBubblesVisibility(int index)
        {
            switch (index)
            {
                case (int)ChatBubbleVisibilitySettings.All:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.All);
                    return true;
                case (int)ChatBubbleVisibilitySettings.NearbyOnly:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.NearbyOnly);
                    return true;
                case (int)ChatBubbleVisibilitySettings.None:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.None);
                    return true;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatBubblesVisibilityController: {index}");
                    return false;
            }
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSettings);
        }
    }
}
