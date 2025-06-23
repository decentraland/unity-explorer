using DCL.Prefs;

namespace DCL.Settings
{
    public class SettingsDataStore : ISettingsDataStore
    {
        public bool HasKey(string key) =>
            DCLPlayerPrefs.HasKey(key);

        public void SetToggleValue(string key, bool value, bool save = false)
        {
            DCLPlayerPrefs.SetInt(key, value ? 1 : 0);

            if (save)
                DCLPlayerPrefs.Save();
        }

        public bool GetToggleValue(string key) =>
            DCLPlayerPrefs.GetInt(key) == 1;

        public void SetSliderValue(string key, float value, bool save = false)
        {
            DCLPlayerPrefs.SetFloat(key, value);

            if (save)
                DCLPlayerPrefs.Save();
        }

        public float GetSliderValue(string key) =>
            DCLPlayerPrefs.GetFloat(key);

        public void SetDropdownValue(string key, int value, bool save = false)
        {
            DCLPlayerPrefs.SetInt(key, value);

            if (save)
                DCLPlayerPrefs.Save();
        }

        public int GetDropdownValue(string key) =>
            DCLPlayerPrefs.GetInt(key);

        public void Save()
        {
            DCLPlayerPrefs.Save();
        }
    }
}
