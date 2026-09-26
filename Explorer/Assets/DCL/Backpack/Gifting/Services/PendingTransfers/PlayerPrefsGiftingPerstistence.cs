using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using DCL.Prefs;
using DCL.Web3.Identities;

namespace DCL.Backpack.Gifting.Services
{
    public class PlayerPrefsGiftingPersistence : IGiftingPersistence
    {
        // Separates entries; URNs never contain it.
        private const char ENTRY_SEPARATOR = ';';

        // Separates the full URN from its transferred-at baseline (ticks) and gift kind; URNs never contain it.
        private const char FIELD_SEPARATOR = '|';

        private readonly IWeb3IdentityCache identityCache;

        public PlayerPrefsGiftingPersistence(IWeb3IdentityCache identityCache)
        {
            this.identityCache = identityCache;
        }

        private string GetUserPrefKey()
        {
            if (identityCache.Identity != null)
                return string.Format(DCLPrefKeys.GIFTING_PENDING_GIFTS, identityCache.Identity.Address);

            ReportHub.LogWarning(ReportCategory.GIFTING, "Cannot load/save pending items: no user is logged in.");

            return string.Empty;
        }

        public void SavePendingUrns(IReadOnlyDictionary<string, PendingTransfer> entries)
        {
            string userKey = GetUserPrefKey();
            if (string.IsNullOrEmpty(userKey)) return;

            var sb = new StringBuilder();
            foreach (KeyValuePair<string, PendingTransfer> entry in entries)
            {
                if (sb.Length > 0) sb.Append(ENTRY_SEPARATOR);

                sb.Append(entry.Key)
                  .Append(FIELD_SEPARATOR).Append(entry.Value.BaselineTransferredAt.Ticks)
                  .Append(FIELD_SEPARATOR).Append(entry.Value.Kind.HasValue ? ((int)entry.Value.Kind.Value).ToString() : string.Empty);
            }

            DCLPlayerPrefs.SetString(userKey, sb.ToString());
            DCLPlayerPrefs.Save();
        }

        public Dictionary<string, PendingTransfer> LoadPendingUrns()
        {
            var result = new Dictionary<string, PendingTransfer>();

            string userKey = GetUserPrefKey();
            if (string.IsNullOrEmpty(userKey) || !DCLPlayerPrefs.HasKey(userKey)) return result;
            string savedData = DCLPlayerPrefs.GetString(userKey);

            if (string.IsNullOrEmpty(savedData)) return result;

            string[] entries = savedData.Split(ENTRY_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entry in entries)
            {
                string[] fields = entry.Split(FIELD_SEPARATOR);

                string fullUrn = fields[0];
                if (string.IsNullOrEmpty(fullUrn)) continue;

                // Drop baseline-less legacy (URN-only) entries: without a baseline Prune can't tell "indexer
                // behind" from "gifted back". Safe — the per-user pref key change already orphaned them.
                if (fields.Length < 2 || !long.TryParse(fields[1], out long ticks) || ticks <= DateTime.MinValue.Ticks)
                    continue;

                // Interim format had no kind; it stays null for those.
                var baseline = new DateTime(ticks);
                GiftableType? kind = fields.Length > 2 && int.TryParse(fields[2], out int kindValue) ? (GiftableType)kindValue : null;

                result[fullUrn] = new PendingTransfer(baseline, kind);
            }

            return result;
        }
    }
}
