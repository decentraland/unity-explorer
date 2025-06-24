using DCL.Prefs;

namespace DCL.Settings
{
    public class SettingsDataStore
    {
        public static bool HasKey(string key) =>
            DCLPlayerPrefs.HasKey(key);

        public static void SetToggleValue(string key, bool value, bool save = false)
        {
            DCLPlayerPrefs.SetInt(key, value ? 1 : 0);

            if (save)
                DCLPlayerPrefs.Save();
        }

        public static bool GetToggleValue(string key) =>
            DCLPlayerPrefs.GetInt(key) == 1;

        public static void SetSliderValue(string key, float value, bool save = false)
        {
            DCLPlayerPrefs.SetFloat(key, value);

            if (save)
                DCLPlayerPrefs.Save();
        }

        public static float GetSliderValue(string key) =>
            DCLPlayerPrefs.GetFloat(key);

        public static void SetDropdownValue(string key, int value, bool save = false)
        {
            DCLPlayerPrefs.SetInt(key, value);

            if (save)
                DCLPlayerPrefs.Save();
        }

        public static int GetDropdownValue(string key) =>
            DCLPlayerPrefs.GetInt(key);

        public static void Save() =>
            DCLPlayerPrefs.Save();
    }
}
