using DCL.Prefs;
using System;
using System.Collections.Generic;

namespace DCL.Settings.Settings
{
    /// <summary>
    ///     Which of the configured lobby stage looks the user picked; the choice is stored by display name and the first entry is the default.
    /// </summary>
    public class LobbyStagePresetSetting
    {
        public IReadOnlyList<string> Names { get; }

        /// <summary>
        ///     Index into <see cref="Names" /> of the current choice, or -1 when there is nothing to choose from.
        /// </summary>
        public int SelectedIndex { get; private set; }

        public event Action<int>? SelectedChanged;

        public LobbyStagePresetSetting(IReadOnlyList<string> names)
        {
            Names = names;
            SelectedIndex = IndexOfStored();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Names.Count || index == SelectedIndex) return;

            SelectedIndex = index;
            DCLPlayerPrefs.SetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET, Names[index], save: true);
            SelectedChanged?.Invoke(index);
        }

        // A stored name no longer in the list falls back to the default rather than to nothing
        private int IndexOfStored()
        {
            if (Names.Count == 0) return -1;

            string stored = DCLPlayerPrefs.GetString(DCLPrefKeys.SETTINGS_LOBBY_STAGE_PRESET);

            for (var i = 0; i < Names.Count; i++)
                if (Names[i] == stored)
                    return i;

            return 0;
        }
    }
}
