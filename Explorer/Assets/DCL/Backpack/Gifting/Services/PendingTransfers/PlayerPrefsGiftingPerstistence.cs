using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using DCL.Prefs;
using DCL.Web3.Identities;

namespace DCL.Backpack.Gifting.Services
{
    public class PlayerPrefsGiftingPersistence : IGiftingPersistence
    {
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

        public void SavePendingUrns(IEnumerable<string> urns)
        {
            string userKey = GetUserPrefKey();
            if (string.IsNullOrEmpty(userKey)) return;

            string data = string.Join(';', urns);
            DCLPlayerPrefs.SetString(userKey, data);
            DCLPlayerPrefs.Save();
        }

        public HashSet<string> LoadPendingUrns()
        {
            var result = new HashSet<string>();

            string userKey = GetUserPrefKey();
            if (string.IsNullOrEmpty(userKey) || !DCLPlayerPrefs.HasKey(userKey)) return result;
            string savedData = DCLPlayerPrefs.GetString(userKey);

            if (string.IsNullOrEmpty(savedData)) return result;

            string[]? split = savedData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in split)
                result.Add(part);

            return result;
        }
    }
}
