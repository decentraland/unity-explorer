using System.Collections.Generic;

namespace DCL.Prefs
{
    public class InMemoryDCLPlayerPrefs : IDCLPrefs
    {
        private readonly Dictionary<string, string> strings = new ();
        private readonly Dictionary<string, int> ints = new ();
        private readonly Dictionary<string, float> floats = new ();

        public void SetString(string key, string value)
        {
            DeleteKey(key);
            strings[key] = value;
        }

        public void SetInt(string key, int value)
        {
            DeleteKey(key);
            ints[key] = value;
        }

        public void SetFloat(string key, float value)
        {
            DeleteKey(key);
            floats[key] = value;
        }

        public string GetString(string key, string defaultValue) =>
            strings.GetValueOrDefault(key, defaultValue);

        public int GetInt(string key, int defaultValue) =>
            ints.GetValueOrDefault(key, defaultValue);

        public float GetFloat(string key, float defaultValue) =>
            floats.GetValueOrDefault(key, defaultValue);

        public bool HasKey(string key) =>
            strings.ContainsKey(key) || ints.ContainsKey(key) || floats.ContainsKey(key);

        public void DeleteKey(string key)
        {
            strings.Remove(key);
            ints.Remove(key);
            floats.Remove(key);
        }

        public void DeleteAll()
        {
            strings.Clear();
            ints.Clear();
            floats.Clear();
        }

        public void Save()
        {
            // Does nothing
        }
    }
}
