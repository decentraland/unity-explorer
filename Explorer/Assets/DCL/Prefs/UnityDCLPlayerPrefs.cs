using System;
using UnityEngine;

namespace DCL.Prefs
{
    internal class UnityDCLPlayerPrefs : IDCLPrefs
    {
        private readonly string prefix;

        public UnityDCLPlayerPrefs(string prefix = null)
        {
            this.prefix = prefix;
        }

        public void SetString(string key, string value) =>
            PlayerPrefs.SetString(prefix + key, value);

        public void SetInt(string key, int value) =>
            PlayerPrefs.SetInt(prefix + key, value);

        public void SetFloat(string key, float value) =>
            PlayerPrefs.SetFloat(prefix + key, value);

        public void SetBool(string key, bool value) =>
            PlayerPrefs.SetInt(key, value ? 1 : 0);

        public string GetString(string key, string defaultValue) =>
            PlayerPrefs.GetString(prefix + key, defaultValue);

        public int GetInt(string key, int defaultValue) =>
            PlayerPrefs.GetInt(prefix + key, defaultValue);

        public float GetFloat(string key, float defaultValue) =>
            PlayerPrefs.GetFloat(prefix + key, defaultValue);

        public bool GetBool(string key, bool defaultValue) =>
            PlayerPrefs.GetInt(prefix + key, defaultValue ? 1 : 0) == 1;

        public bool HasKey(string key) =>
            PlayerPrefs.HasKey(prefix + key);

        public void DeleteKey(string key) =>
            PlayerPrefs.DeleteKey(prefix + key);

        public void DeleteAll() =>
            PlayerPrefs.DeleteAll();

        public void Save() =>
            PlayerPrefs.Save();
    }
}
