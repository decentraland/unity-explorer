using System;
using System.Collections.Generic;
using System.Globalization;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Diagnostics;
using DCL.Prefs;

namespace DCL.Backpack.Gifting.Services
{
    public class PlayerPrefsGiftingPersistence : IGiftingPersistence
    {
        private const char ENTRY_SEPARATOR = ';';
        private const char FIELD_SEPARATOR = '|';
        private const string DATE_FORMAT = "O"; // ISO 8601 round-trip format

        public void SavePendingTransfers(
            IEnumerable<PendingTransferEntry> wearables,
            IEnumerable<PendingTransferEntry> emotes)
        {
            string wearablesData = SerializeEntries(wearables);
            string emotesData = SerializeEntries(emotes);

            DCLPlayerPrefs.SetString(DCLPrefKeys.GIFTING_PENDING_WEARABLES_V2, wearablesData);
            DCLPlayerPrefs.SetString(DCLPrefKeys.GIFTING_PENDING_EMOTES_V2, emotesData);
            DCLPlayerPrefs.Save();
        }

        public (Dictionary<string, PendingTransferEntry> wearables, Dictionary<string, PendingTransferEntry> emotes) LoadPendingTransfers()
        {
            // Check for legacy data and migrate if needed
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.GIFTING_PENDING_GIFTS))
            {
                var migrated = MigrateLegacyData();
                // Delete legacy key after migration
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.GIFTING_PENDING_GIFTS);
                DCLPlayerPrefs.Save();
                return migrated;
            }

            var wearables = LoadEntries(DCLPrefKeys.GIFTING_PENDING_WEARABLES_V2);
            var emotes = LoadEntries(DCLPrefKeys.GIFTING_PENDING_EMOTES_V2);

            return (wearables, emotes);
        }

        private static string SerializeEntries(IEnumerable<PendingTransferEntry> entries)
        {
            var parts = new List<string>();
            foreach (var entry in entries)
            {
                string dateStr = entry.SentAtUtc.ToString(DATE_FORMAT, CultureInfo.InvariantCulture);
                parts.Add($"{entry.FullUrn}{FIELD_SEPARATOR}{dateStr}");
            }
            return string.Join(ENTRY_SEPARATOR, parts);
        }

        private static Dictionary<string, PendingTransferEntry> LoadEntries(string prefKey)
        {
            var result = new Dictionary<string, PendingTransferEntry>(StringComparer.OrdinalIgnoreCase);

            if (!DCLPlayerPrefs.HasKey(prefKey))
                return result;

            string savedData = DCLPlayerPrefs.GetString(prefKey);
            if (string.IsNullOrEmpty(savedData))
                return result;

            string[] entries = savedData.Split(ENTRY_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entryStr in entries)
            {
                int separatorIndex = entryStr.LastIndexOf(FIELD_SEPARATOR);
                if (separatorIndex <= 0)
                {
                    ReportHub.LogWarning(ReportCategory.GIFTING, $"[Persistence] Invalid entry format, skipping: {entryStr}");
                    continue;
                }

                string urn = entryStr.Substring(0, separatorIndex);
                string dateStr = entryStr.Substring(separatorIndex + 1);

                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime sentAt))
                {
                    ReportHub.LogWarning(ReportCategory.GIFTING, $"[Persistence] Could not parse date, using UtcNow: {dateStr}");
                    sentAt = DateTime.UtcNow;
                }

                result[urn] = new PendingTransferEntry(urn, sentAt);
            }

            return result;
        }

        private (Dictionary<string, PendingTransferEntry> wearables, Dictionary<string, PendingTransferEntry> emotes) MigrateLegacyData()
        {
            ReportHub.Log(ReportCategory.GIFTING, "[Persistence] Migrating legacy pending gifts data...");

            var wearables = new Dictionary<string, PendingTransferEntry>(StringComparer.OrdinalIgnoreCase);
            var emotes = new Dictionary<string, PendingTransferEntry>(StringComparer.OrdinalIgnoreCase);

            string savedData = DCLPlayerPrefs.GetString(DCLPrefKeys.GIFTING_PENDING_GIFTS);
            if (string.IsNullOrEmpty(savedData))
                return (wearables, emotes);

            // Legacy format: "urn1;urn2;urn3" (no timestamps, no type distinction)
            string[] urns = savedData.Split(ENTRY_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
            
            // Assign current time as sentAt - the 1-hour timeout will naturally clean these up
            // Assume all legacy entries are wearables (emote gifting is newer feature)
            var now = DateTime.UtcNow;
            foreach (string urn in urns)
            {
                wearables[urn] = new PendingTransferEntry(urn, now);
                ReportHub.Log(ReportCategory.GIFTING, $"[Persistence] Migrated legacy entry as wearable: {urn}");
            }

            // Save migrated data to new format
            SavePendingTransfers(wearables.Values, emotes.Values);

            ReportHub.Log(ReportCategory.GIFTING, $"[Persistence] Migration complete. Migrated {wearables.Count} wearables.");

            return (wearables, emotes);
        }
    }
}
