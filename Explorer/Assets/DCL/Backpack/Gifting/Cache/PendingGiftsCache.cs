using System;
using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
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
        private static readonly HashSet<string> pendingFullUrns;

        static PendingGiftsCache()
        {
            pendingFullUrns = new HashSet<string>();
            Load();
        }

        private static void Load()
        {
            if (!PlayerPrefs.HasKey(PREFS_KEY)) return;
            string savedData = PlayerPrefs.GetString(PREFS_KEY);
            if (string.IsNullOrEmpty(savedData)) return;

            string[]? loadedUrns = savedData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string urn in loadedUrns)
                pendingFullUrns.Add(urn);
        }

        private static void Save()
        {
            string dataToSave = string.Join(';', pendingFullUrns);
            PlayerPrefs.SetString(PREFS_KEY, dataToSave);
        }

        public static void Add(URN fullUrn)
        {
            if (pendingFullUrns.Add(fullUrn.ToString()))
                Save();
        }

        public static bool Contains(URN fullUrn)
        {
            return pendingFullUrns.Contains(fullUrn.ToString());
        }

        public static int GetPendingCount(URN baseUrn)
        {
            int count = 0;
            string baseUrnString = baseUrn.ToString();
            foreach (string pendingFullUrn in pendingFullUrns)
            {
                if (pendingFullUrn.StartsWith(baseUrnString) && pendingFullUrn.Length > baseUrnString.Length && pendingFullUrn[baseUrnString.Length] == ':')
                    count++;
            }

            return count;
        }

        public static void Prune(Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>>? allOwnedWearables,
            Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>>? allOwnedEmotes)
        {
            var allActualFullUrns = new HashSet<string>();

            if (allOwnedWearables != null)
                foreach (var registry in allOwnedWearables.Values)
                foreach (var entry in registry.Values)
                    allActualFullUrns.Add(entry.Urn);

            if (allOwnedEmotes != null)
                foreach (var registry in allOwnedEmotes.Values)
                foreach (var entry in registry.Values)
                    allActualFullUrns.Add(entry.Urn);

            int itemsRemoved = pendingFullUrns.RemoveWhere(pendingUrn => !allActualFullUrns.Contains(pendingUrn));

            if (itemsRemoved > 0)
            {
                ReportHub.Log(ReportCategory.GIFTING, $"Pruned {itemsRemoved} confirmed gifts from the pending cache.");
                Save();
            }
        }
    }
}