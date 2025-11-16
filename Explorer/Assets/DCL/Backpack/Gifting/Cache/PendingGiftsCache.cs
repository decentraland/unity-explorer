using System;
using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using UnityEngine;

namespace DCL.Backpack.Gifting.Cache
{
    /// <summary>
    ///     Manages a persistent list of URNs for gifts that have been successfully sent
    ///     but may not have propagated through the backend yet. This prevents users from
    ///     seeing and trying to re-gift items that are in a pending transfer state.
    /// </summary>
    public static class PendingGiftsCache
    {
        private const string PREFS_KEY = "PendingGifts";
        private static readonly HashSet<string> pendingUrns;

        static PendingGiftsCache()
        {
            pendingUrns = new HashSet<string>();
            Load();
        }

        private static void Load()
        {
            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                string savedData = PlayerPrefs.GetString(PREFS_KEY);
                if (string.IsNullOrEmpty(savedData)) return;

                string[]? loadedUrns = savedData.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (string urn in loadedUrns)
                    pendingUrns.Add(urn);
            }
        }

        private static void Save()
        {
            string dataToSave = string.Join(';', pendingUrns);
            PlayerPrefs.SetString(PREFS_KEY, dataToSave);
            PlayerPrefs.Save();
        }

        /// <summary>
        ///     Adds a URN to the pending cache after a successful gift transfer.
        /// </summary>
        public static void Add(URN urn)
        {
            if (pendingUrns.Add(urn.ToString()))
                Save();
        }

        /// <summary>
        ///     Checks if a URN is currently in the pending transfer cache.
        /// </summary>
        public static bool Contains(URN urn)
        {
            return pendingUrns.Contains(urn.ToString());
        }

        /// <summary>
        ///     Compares the pending cache against the full list of actually owned items from the backend.
        ///     Any pending item that is no longer owned is considered fully transferred and is removed from the cache.
        /// </summary>
        public static void Prune(IReadOnlyCollection<string> actualOwnedUrns)
        {
            var actuals = new HashSet<string>(actualOwnedUrns);
            int itemsRemoved = pendingUrns.RemoveWhere(pending => !actuals.Contains(pending));

            if (itemsRemoved > 0)
            {
                ReportHub.Log(ReportCategory.GIFTING, $"Pruned {itemsRemoved} confirmed gifts from the pending cache.");
                Save();
            }
        }
    }
}