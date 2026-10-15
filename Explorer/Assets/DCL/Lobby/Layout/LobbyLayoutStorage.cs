using DCL.Diagnostics;
using DCL.Prefs;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Persists the lobby layout as one JSON string in the player prefs. Widgets and corners are stored by enum name so a
    ///     renamed or resized widget is dropped on load instead of landing on a wrong one. A stored layout without
    ///     <see cref="LobbyLayout.REQUIRED_WIDGET" /> gets it back on load.
    /// </summary>
    public static class LobbyLayoutStorage
    {
        private const int VERSION = 1;

        public static void Save(LobbyLayout layout)
        {
            IReadOnlyList<LobbyWidgetPlacement> placements = layout.Placements;
            var dto = new LayoutDto { Version = VERSION, Entries = new List<EntryDto>(placements.Count) };

            for (var i = 0; i < placements.Count; i++)
            {
                LobbyWidgetPlacement placement = placements[i];

                dto.Entries.Add(new EntryDto
                {
                    Corner = placement.Corner.ToString(),
                    Widget = placement.WidgetId.ToString(),
                    X = placement.Cell.x,
                    Y = placement.Cell.y,
                });
            }

            DCLPlayerPrefs.SetString(DCLPrefKeys.LOBBY_LAYOUT, JsonUtility.ToJson(dto), save: true);
        }

        /// <summary>
        ///     Replaces the placements with the stored ones. Entries that are unknown, out of the grid or overlapping are
        ///     skipped, and <see cref="LobbyLayout.REQUIRED_WIDGET" /> is put back if no entry places it; a missing,
        ///     malformed or outdated value gives the default layout.
        /// </summary>
        public static void Load(LobbyLayout layout)
        {
            layout.Clear();

            if (!DCLPlayerPrefs.HasKey(DCLPrefKeys.LOBBY_LAYOUT))
            {
                layout.ResetToDefault();
                return;
            }

            LayoutDto? dto;

            try { dto = JsonUtility.FromJson<LayoutDto>(DCLPlayerPrefs.GetString(DCLPrefKeys.LOBBY_LAYOUT)); }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.UI, $"The stored lobby layout is malformed, the default layout is used: {e.Message}");
                layout.ResetToDefault();
                return;
            }

            if (dto == null || dto.Version != VERSION || dto.Entries == null)
            {
                layout.ResetToDefault();
                return;
            }

            for (var i = 0; i < dto.Entries.Count; i++)
            {
                EntryDto entry = dto.Entries[i];

                if (!TryParseDefined(entry.Widget, out LobbyWidgetId widgetId) || !TryParseDefined(entry.Corner, out LobbyGridCorner corner))
                    continue;

                layout.TryPlace(widgetId, corner, new Vector2Int(entry.X, entry.Y));
            }

            layout.EnsureRequiredWidget();
        }

        // Enum.TryParse also accepts any integer, so the result is checked against the declared values
        private static bool TryParseDefined<T>(string? name, out T value) where T: struct, Enum =>
            Enum.TryParse(name, out value) && Enum.IsDefined(typeof(T), value);

        [Serializable]
        private class LayoutDto
        {
            public int Version;
            public List<EntryDto>? Entries;
        }

        [Serializable]
        private class EntryDto
        {
            public string? Corner;
            public string? Widget;
            public int X;
            public int Y;
        }
    }
}
