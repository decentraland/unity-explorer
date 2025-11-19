using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Backpack.Gifting.Services
{
    public class PlayerPrefsGiftingPersistence : IGiftingPersistence
    {
        private const string PREFS_KEY = "PendingGifts";

        public void SavePendingUrns(IEnumerable<string> urns)
        {
            string data = string.Join(';', urns);
            PlayerPrefs.SetString(PREFS_KEY, data);
            PlayerPrefs.Save();
        }

        public HashSet<string> LoadPendingUrns()
        {
            var result = new HashSet<string>();
            if (!PlayerPrefs.HasKey(PREFS_KEY)) return result;

            string savedData = PlayerPrefs.GetString(PREFS_KEY);
            if (string.IsNullOrEmpty(savedData)) return result;

            string[]? split = savedData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < split.Length; i++)
            {
                result.Add(split[i]);
            }

            return result;
        }
    }
}