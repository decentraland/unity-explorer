using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.Prefs
{
    public class FileDCLPlayerPrefs : IDCLPrefs
    {
        private const int CONCURRENT_CLIENTS = 16;
        private const string PREFS_FILENAME = "userdata_{0}.json";

        private readonly FileStream fileStream;
        private readonly UserData userData;

        private readonly UnityDCLPlayerPrefs unityPrefs;

        private bool dataChanged;

        public FileDCLPlayerPrefs()
        {
            for (var i = 0; i < CONCURRENT_CLIENTS; i++)
                try
                {
                    string path = Path.Combine(Application.persistentDataPath, string.Format(PREFS_FILENAME, i));

                    // Note that FileShare.None should lock the file to other processes, and it does,
                    // but only on Windows. And .Lock(0, 0) does the same, but only on MacOS.
                    fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    fileStream.Lock(0, 0);

                    // We use this to migrate existing keys, but only for the first running instance
                    if (i == 0)
                        unityPrefs = new UnityDCLPlayerPrefs();

                    break;
                }
                catch (IOException) { fileStream?.Dispose(); }

            if (fileStream == null)
                throw new Exception("Failed to open unityPrefs file");

            // Load data
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 1024, true);
            string json = reader.ReadToEnd();
            userData = JsonConvert.DeserializeObject<UserData>(json) ?? new UserData();
        }

        public void SetString(string key, string value)
        {
            if (userData.Strings.TryGetValue(key, out string existing) && existing == value) return;

            userData.Strings[key] = value;
            dataChanged = true;
        }

        public void SetInt(string key, int value)
        {
            if (userData.Ints.TryGetValue(key, out int existing) && existing == value) return;

            userData.Ints[key] = value;
            dataChanged = true;
        }

        public void SetFloat(string key, float value)
        {
            if (userData.Floats.TryGetValue(key, out float existing) && Mathf.Approximately(existing, value)) return;

            userData.Floats[key] = value;
            dataChanged = true;
        }

        public string GetString(string key, string defaultValue)
        {
            MigrateString(key);
            return userData.Strings.GetValueOrDefault(key, defaultValue);
        }

        public int GetInt(string key, int defaultValue)
        {
            MigrateInt(key);
            return userData.Ints.GetValueOrDefault(key, defaultValue);
        }

        public float GetFloat(string key, float defaultValue)
        {
            MigrateFloat(key);
            return userData.Floats.GetValueOrDefault(key, defaultValue);
        }

        public bool HasKey(string key) =>
            (unityPrefs != null && unityPrefs.HasKey(key)) || userData.Strings.ContainsKey(key) || userData.Ints.ContainsKey(key) || userData.Floats.ContainsKey(key);

        public void DeleteKey(string key)
        {
            if (!HasKey(key)) return;

            userData.Strings.Remove(key);
            userData.Ints.Remove(key);
            userData.Floats.Remove(key);
            PlayerPrefs.DeleteKey(key);
            dataChanged = true;
        }

        public void DeleteAll()
        {
            userData.Strings.Clear();
            userData.Ints.Clear();
            userData.Floats.Clear();
        }

        public void Save()
        {
            if (!dataChanged) return;

            dataChanged = false;

            // Run save on a background thread
            Task.Run(() =>
            {
                lock (fileStream)
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    fileStream.SetLength(0);

                    using var writer = new StreamWriter(fileStream, Encoding.UTF8, 1024, true);
                    string json = JsonConvert.SerializeObject(userData);
                    writer.Write(json);
                    writer.Flush();
                }
            });
        }

        private void MigrateString(string key)
        {
            if (unityPrefs == null || !unityPrefs.HasKey(key)) return;

            userData.Strings.Add(key, unityPrefs!.GetString(key, null));
            unityPrefs.DeleteKey(key);
            unityPrefs.Save();
        }

        private void MigrateInt(string key)
        {
            if (unityPrefs == null || !unityPrefs.HasKey(key)) return;

            userData.Ints.Add(key, unityPrefs!.GetInt(key, 0));
            unityPrefs.DeleteKey(key);
            unityPrefs.Save();
        }

        private void MigrateFloat(string key)
        {
            if (unityPrefs == null || !unityPrefs.HasKey(key)) return;

            userData.Floats.Add(key, unityPrefs!.GetFloat(key, 0f));
            unityPrefs.DeleteKey(key);
            unityPrefs.Save();
        }

        [Serializable]
        private class UserData
        {
            public Dictionary<string, string> Strings { get; private set; } = new ();
            public Dictionary<string, int> Ints { get; private set; } = new ();
            public Dictionary<string, float> Floats { get; private set; } = new ();
        }
    }
}
