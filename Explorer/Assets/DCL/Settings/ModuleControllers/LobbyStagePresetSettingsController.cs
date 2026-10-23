using DCL.FeatureFlags;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using System.Collections.Generic;
using TMPro;

namespace DCL.Settings.ModuleControllers
{
    /// <summary>
    ///     Dropdown of the lobby stage looks; it only reacts while the lobby toggle is on.
    /// </summary>
    public class LobbyStagePresetSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly LobbyStagePresetSetting setting;
        private readonly ISettingsModuleEventListener settingsEventListener;

        public LobbyStagePresetSettingsController(SettingsDropdownModuleView view, LobbyStagePresetSetting setting, ISettingsModuleEventListener settingsEventListener)
        {
            this.view = view;
            this.setting = setting;
            this.settingsEventListener = settingsEventListener;

            TMP_Dropdown dropdown = view.DropdownView.Dropdown;
            dropdown.ClearOptions();

            var options = new List<TMP_Dropdown.OptionData>(setting.Names.Count);

            foreach (string name in setting.Names)
                options.Add(new TMP_Dropdown.OptionData(name));

            dropdown.AddOptions(options);

            if (setting.SelectedIndex >= 0)
                dropdown.SetValueWithoutNotify(setting.SelectedIndex);

            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(setting.Select);

            // SetViewInteractable is unusable here: the view is attached to the controller after construction
            view.SetInteractable(IsInteractable(FeaturesRegistry.Instance.LobbyEnabledSetting));
            settingsEventListener.LobbyEnabledChanged += OnLobbyEnabledChanged;
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(setting.Select);
            settingsEventListener.LobbyEnabledChanged -= OnLobbyEnabledChanged;
        }

        private void OnLobbyEnabledChanged(bool _, bool enabled) =>
            view.SetInteractable(IsInteractable(enabled));

        private bool IsInteractable(bool lobbyEnabled) =>
            lobbyEnabled && setting.Names.Count > 0;
    }
}
