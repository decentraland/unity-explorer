using System;
using System.Collections.Generic;
using DCL.Prefs;

namespace DCL.Backpack.Gifting.Services
{
    public class PlayerPrefsGiftingPersistence : IGiftingPersistence
    {
        public void SavePendingUrns(IEnumerable<string> urns)
        {
            string data = string.Join(';', urns);
            DCLPlayerPrefs.SetString(DCLPrefKeys.GIFTING_PENDING_GIFTS, data);
            DCLPlayerPrefs.Save();
        }

        public HashSet<string> LoadPendingUrns()
        {
            var result = new HashSet<string>();
            if (!DCLPlayerPrefs.HasKey(DCLPrefKeys.GIFTING_PENDING_GIFTS)) return result;
            string savedData = DCLPlayerPrefs.GetString(DCLPrefKeys.GIFTING_PENDING_GIFTS);
            
            if (string.IsNullOrEmpty(savedData)) return result;

            string[]? split = savedData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in split)
                result.Add(part);

            return result;
        }
    }
}