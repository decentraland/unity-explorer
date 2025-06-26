using System;
using System.IO;
using System.Linq;
using Unity.Multiplayer.Playmode;
using UnityEditor;
using UnityEngine;

namespace DCL.Prefs
{
    /// <summary>
    /// A wrapper based on UnityEngine.PlayerPrefs for storing preferences / settings.
    /// </summary>
    public static class DCLPlayerPrefs
    {
        private static IDCLPrefs dclPrefs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            string[] playmodeTags = CurrentPlayer.ReadOnlyTags();
            Initialize(playmodeTags.Contains("PrefsInMemory"));
        }

        public static void SetString(string key, string value) =>
            dclPrefs.SetString(key, value);

        public static void SetInt(string key, int value, bool save = false)
        {
            dclPrefs.SetInt(key, value);

            if (save)
                Save();
        }

        public static void SetFloat(string key, float value, bool save = false)
        {
            dclPrefs.SetFloat(key, value);

            if (save)
                Save();
        }

        public static string GetString(string key, string defaultValue = "") =>
            dclPrefs.GetString(key, defaultValue);

        public static int GetInt(string key, int defaultValue = 0) =>
            dclPrefs.GetInt(key, defaultValue);

        public static float GetFloat(string key, float defaultValue = 0f) =>
            dclPrefs.GetFloat(key, defaultValue);

        public static bool HasKey(string key) =>
            dclPrefs.HasKey(key);

        public static void DeleteKey(string key) =>
            dclPrefs.DeleteKey(key);

        public static void SetBool(string key, bool value, bool save = false)
        {
            dclPrefs.SetBool(key, value);

            if (save)
                Save();
        }

        public static bool GetBool(string key, bool defaultValue = false) =>
            dclPrefs.GetBool(key, defaultValue);

        public static void DeleteAll() =>
            dclPrefs.DeleteAll();

        public static void Save() =>
            dclPrefs.Save();

        private static void Initialize(bool inMemory)
        {
            if (dclPrefs != null)
                throw new InvalidOperationException("DCLPrefs already initialized.");

            dclPrefs = inMemory ? new InMemoryDCLPlayerPrefs() : new FileDCLPlayerPrefs();
        }

#if UNITY_EDITOR
        [MenuItem("Edit/Clear All DCLPlayerPrefs", priority = 280)]
        private static void ClearDCLPlayerPrefs()
        {
            string[] files = Directory.GetFiles(Application.persistentDataPath, "userdata_*");

            foreach (string file in files)
                File.Delete(file);
        }

        [MenuItem("Edit/Clear All DCLPlayerPrefs", validate = true)]
        private static bool ValidateClearDCLPlayerPrefs() =>
            !Application.isPlaying;
#endif
    }
}
