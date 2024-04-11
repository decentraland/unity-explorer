using UnityEngine;

namespace DCL.Settings
{
    public class SettingsDataStore : ISettingsDataStore
    {
        public void SetToggleValue(string key, bool value, bool save = false)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);

            if (save)
                PlayerPrefs.Save();
        }

        public bool GetToggleValue(string key, bool defaultValue) =>
            PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;

        public void SetSliderValue(string key, float value, bool save = false)
        {
            PlayerPrefs.SetFloat(key, value);

            if (save)
                PlayerPrefs.Save();
        }

        public float GetSliderValue(string key, float defaultValue) =>
            PlayerPrefs.GetFloat(key, defaultValue);

        public void SetDropdownValue(string key, int value, bool save = false)
        {
            PlayerPrefs.SetInt(key, value);

            if (save)
                PlayerPrefs.Save();
        }

        public int GetDropdownValue(string key, int defaultValue) =>
            PlayerPrefs.GetInt(key, defaultValue);

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
