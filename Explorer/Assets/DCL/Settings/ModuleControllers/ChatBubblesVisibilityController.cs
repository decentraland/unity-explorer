using DCL.Diagnostics;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class ChatBubblesVisibilityController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly ChatSettingsAsset chatSettingsAsset;

        public ChatBubblesVisibilityController(SettingsDropdownModuleView view, ChatSettingsAsset chatSettingsAsset)
        {
            this.view = view;
            this.chatSettingsAsset = chatSettingsAsset;

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetSettings);
        }

        private void SetSettings(int index)
        {
            switch (index)
            {
                case (int)ChatBubbleVisibilitySettings.ALL:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.ALL);
                    break;
                case (int)ChatBubbleVisibilitySettings.NEARBY_ONLY:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.NEARBY_ONLY);
                    break;
                case (int)ChatBubbleVisibilitySettings.NONE:
                    chatSettingsAsset.SetBubblesVisibility(ChatBubbleVisibilitySettings.NONE);
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatPrivacySettingsController: {index}");
                    return;
            }
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSettings);
        }
    }
}
