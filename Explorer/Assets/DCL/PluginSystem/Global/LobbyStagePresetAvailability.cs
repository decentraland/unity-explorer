using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using StagePresetEntry = DCL.PluginSystem.Global.LobbyPlugin.LobbyPluginSettings.StagePresetEntry;

// ReSharper disable InconsistentNaming
namespace DCL.PluginSystem.Global
{
    public static class LobbyStagePresetAvailability
    {
        // alfa-lobby-backgrounds payload: {"default":"Game World","available":["Game World","Halloween 2026"]}
        [Serializable]
        public class Config
        {
#pragma warning disable UAC1001 // Newtonsoft-only feature-flag payload (TryGetJsonPayload -> JsonConvert); Unity serialization never reads this class.
            public string? @default;
            public string[]? available;
#pragma warning restore UAC1001
        }

        // The setting treats the first entry as the default
        public static StagePresetEntry[] Resolve(StagePresetEntry[] entries, Config? config)
        {
            StagePresetEntry[] available = Filter(entries, config?.available);
            string? defaultName = config?.@default;

            if (string.IsNullOrEmpty(defaultName)) return available;

            int defaultIndex = IndexOf(available, defaultName);

            if (defaultIndex < 0)
            {
                ReportHub.LogWarning(ReportCategory.UI, $"Default lobby background \"{defaultName}\" is not available, using \"{(available.Length > 0 ? available[0].Name : string.Empty)}\"");
                return available;
            }

            // Copied so the settings asset's own array is never reordered
            var ordered = new StagePresetEntry[available.Length];
            ordered[0] = available[defaultIndex];
            Array.Copy(available, 0, ordered, 1, defaultIndex);
            Array.Copy(available, defaultIndex + 1, ordered, defaultIndex + 1, available.Length - defaultIndex - 1);
            return ordered;
        }

        private static StagePresetEntry[] Filter(StagePresetEntry[] entries, string[]? available)
        {
            if (available == null || available.Length == 0) return entries;

            var kept = new List<StagePresetEntry>(entries.Length);

            foreach (StagePresetEntry entry in entries)
                if (Array.IndexOf(available, entry.Name) >= 0)
                    kept.Add(entry);

            foreach (string name in available)
                if (IndexOf(entries, name) < 0)
                    ReportHub.LogWarning(ReportCategory.UI, $"Lobby background \"{name}\" from the feature flag is not configured, skipping it");

            if (kept.Count > 0) return kept.ToArray();

            // Never leave the lobby without a backdrop
            ReportHub.LogWarning(ReportCategory.UI, "None of the lobby backgrounds from the feature flag are configured, offering all of them");
            return entries;
        }

        private static int IndexOf(StagePresetEntry[] entries, string name)
        {
            for (var i = 0; i < entries.Length; i++)
                if (entries[i].Name == name)
                    return i;

            return -1;
        }
    }
}
