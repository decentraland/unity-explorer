using DCL.Lobby;
using DCL.Settings.Settings;
using System;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Keeps the lobby stage on the look chosen in Settings, applying it as soon as both the choice and its asset are known.
    /// </summary>
    public class LobbyStagePresetSwitcher : IDisposable
    {
        private readonly LobbyStage stage;
        private readonly LobbyStagePresetCatalog catalog;
        private readonly LobbyStagePresetSetting setting;

        public LobbyStagePresetSwitcher(LobbyStage stage, LobbyStagePresetCatalog catalog, LobbyStagePresetSetting setting)
        {
            this.stage = stage;
            this.catalog = catalog;
            this.setting = setting;
        }

        public void Dispose()
        {
            setting.SelectedChanged -= Apply;
            catalog.PresetLoaded -= OnPresetLoaded;
        }

        public void Start()
        {
            setting.SelectedChanged += Apply;
            catalog.PresetLoaded += OnPresetLoaded;
            Apply(setting.SelectedIndex);
        }

        private void OnPresetLoaded(int index)
        {
            if (index == setting.SelectedIndex)
                Apply(index);
        }

        private void Apply(int index)
        {
            if (index >= 0 && catalog.PresetAt(index) is { } preset)
                stage.ApplyPreset(preset);
        }
    }
}
