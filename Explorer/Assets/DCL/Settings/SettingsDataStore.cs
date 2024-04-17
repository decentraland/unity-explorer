using System;
using UnityEngine;

namespace DCL.Settings
{
    public class SettingsDataStore : ISettingsDataStore
    {
        public bool HasKey(string key) =>
            PlayerPrefs.HasKey(key);

        public void SetToggleValue(string key, bool value, bool save = false)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);

            if (save)
                PlayerPrefs.Save();
        }

        public bool GetToggleValue(string key) =>
            PlayerPrefs.GetInt(key) == 1;

        public void SetSliderValue(string key, float value, bool save = false)
        {
            PlayerPrefs.SetFloat(key, value);

            if (save)
                PlayerPrefs.Save();
        }

        public float GetSliderValue(string key) =>
            PlayerPrefs.GetFloat(key);

        public void SetDropdownValue(string key, int value, bool save = false)
        {
            PlayerPrefs.SetInt(key, value);

            if (save)
                PlayerPrefs.Save();
        }

        public int GetDropdownValue(string key) =>
            PlayerPrefs.GetInt(key);

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
