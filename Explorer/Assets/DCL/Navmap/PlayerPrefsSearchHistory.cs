using DCL.Prefs;
using System;
using System.Linq;

namespace DCL.Navmap
{
    public class PlayerPrefsSearchHistory : ISearchHistory
    {
        private const string PREVIOUS_SEARCHES_KEY = "previous_searches";
        private const int MAX_PREVIOUS_SEARCHES = 5;

        public void Add(string search)
        {
            string previousSearchesStr = DCLPlayerPrefs.GetString(PREVIOUS_SEARCHES_KEY);
            string[] previousSearches = string.IsNullOrEmpty(previousSearchesStr) ? Array.Empty<string>() : previousSearchesStr.Split('|');

            switch (previousSearches.Length)
            {
                case > 0 when previousSearches[0] == search:
                    return;
                case < MAX_PREVIOUS_SEARCHES:
                    DCLPlayerPrefs.SetString(PREVIOUS_SEARCHES_KEY, previousSearches.Length > 0 ? $"{search}|{previousSearchesStr}" : search);
                    break;
                default:
                    DCLPlayerPrefs.SetString(PREVIOUS_SEARCHES_KEY, $"{search}|{string.Join("|", previousSearches.Take(MAX_PREVIOUS_SEARCHES - 1))}");
                    break;
            }
        }

        public string[] Get()
        {
            string previousSearchesStr = DCLPlayerPrefs.GetString(PREVIOUS_SEARCHES_KEY, "");
            return string.IsNullOrEmpty(previousSearchesStr) ? Array.Empty<string>() : previousSearchesStr.Split('|');
        }
    }
}
