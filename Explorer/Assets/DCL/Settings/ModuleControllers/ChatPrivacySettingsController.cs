using DCL.Diagnostics;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    public class ChatPrivacySettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly ChatSettingsAsset chatSettingsAsset;

        public ChatPrivacySettingsController(SettingsDropdownModuleView view, ChatSettingsAsset chatSettingsAsset)
        {
            this.view = view;
            this.chatSettingsAsset = chatSettingsAsset;

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetSettings);
            this.chatSettingsAsset.PrivacySettingsRead += OnReadSettings;
        }

        private void OnReadSettings(ChatPrivacySettings settings)
        {
            view.DropdownView.Dropdown.value = (int)settings;
        }

        private void SetSettings(int index)
        {
            switch (index)
            {
                case (int)ChatPrivacySettings.ALL:
                    chatSettingsAsset.OnPrivacySet(ChatPrivacySettings.ALL);
                    break;
                case (int)ChatPrivacySettings.ONLY_FRIENDS:
                    chatSettingsAsset.OnPrivacySet(ChatPrivacySettings.ONLY_FRIENDS);
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.SETTINGS_MENU, $"Invalid index value for ChatPrivacySettingsController: {index}");
                    return;
            }
        }

        public override void Dispose()
        {
            this.chatSettingsAsset.PrivacySettingsRead -= OnReadSettings;
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSettings);
        }
    }
}
