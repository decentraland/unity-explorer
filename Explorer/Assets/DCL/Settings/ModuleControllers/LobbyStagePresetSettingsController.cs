using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;

namespace DCL.Settings.ModuleControllers
{
    /// <summary>
    ///     Mirrors the stage preset setting in the background picker, previewing the hovered look over the chosen one.
    /// </summary>
    public class LobbyStagePresetSettingsController : SettingsFeatureController
    {
        private readonly SettingsLobbyStagePresetModuleView view;
        private readonly LobbyStagePresetSetting setting;
        private readonly LobbyStagePresetEntryView[] entries;

        private int hoveredIndex = -1;

        public LobbyStagePresetSettingsController(SettingsLobbyStagePresetModuleView view, LobbyStagePresetSetting setting)
        {
            this.view = view;
            this.setting = setting;
            entries = new LobbyStagePresetEntryView[setting.Names.Count];

            for (var i = 0; i < entries.Length; i++)
            {
                int index = i;
                LobbyStagePresetEntryView entry = view.CreateEntry();
                entry.Tooltip.Configure(setting.Names[index]);
                entry.SetTexture(setting.BackdropOf(index));
                entry.Button.onClick.AddListener(() => setting.Select(index));
                entry.HoverChanged = hovered => OnHoverChanged(index, hovered);
                entries[index] = entry;
            }

            ApplySelection();
            setting.SelectedChanged += OnSelectedChanged;
            setting.BackdropChanged += OnBackdropChanged;
        }

        public override void Dispose()
        {
            setting.SelectedChanged -= OnSelectedChanged;
            setting.BackdropChanged -= OnBackdropChanged;

            foreach (LobbyStagePresetEntryView entry in entries)
            {
                entry.Button.onClick.RemoveAllListeners();
                entry.HoverChanged = null;
            }
        }

        private void OnSelectedChanged(int _) =>
            ApplySelection();

        private void OnBackdropChanged(int index)
        {
            entries[index].SetTexture(setting.BackdropOf(index));
            RefreshPreview();
        }

        private void OnHoverChanged(int index, bool hovered)
        {
            if (hovered)
                hoveredIndex = index;
            else if (hoveredIndex == index)
                hoveredIndex = -1;

            RefreshPreview();
        }

        private void ApplySelection()
        {
            int selected = setting.SelectedIndex;

            for (var i = 0; i < entries.Length; i++)
                entries[i].SetSelected(i == selected);

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            int shown = hoveredIndex >= 0 ? hoveredIndex : setting.SelectedIndex;
            view.SetPreview(shown >= 0 ? setting.BackdropOf(shown) : null);
        }
    }
}
