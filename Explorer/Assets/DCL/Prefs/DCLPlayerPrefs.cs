using System;
using System.Linq;
using Unity.Multiplayer.Playmode;
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

            if (playmodeTags.Contains("PrefsInMemory"))
                Initialize(Mode.InMemory);
            else if (playmodeTags.Contains("PrefsDiskPrefix1"))
                Initialize(Mode.DiskPrefix1);
            else if (playmodeTags.Contains("PrefsDiskPrefix2"))
                Initialize(Mode.DiskPrefix2);
            else if (playmodeTags.Contains("PrefsDiskPrefix3"))
                Initialize(Mode.DiskPrefix3);
            else
                Initialize(Mode.Disk);
        }

        private static void Initialize(Mode mode)
        {
            if (dclPrefs != null)
                throw new InvalidOperationException("DCLPrefs already initialized.");

            Debug.Log($"DCLPrefs using mode {mode}");

            dclPrefs = mode switch
                       {
                           Mode.Disk => new DefaultDCLPlayerPrefs(),
                           Mode.InMemory => new InMemoryDCLPlayerPrefs(),
                           Mode.DiskPrefix1 => new DefaultDCLPlayerPrefs("playmode1_"),
                           Mode.DiskPrefix2 => new DefaultDCLPlayerPrefs("playmode2_"),
                           Mode.DiskPrefix3 => new DefaultDCLPlayerPrefs("playmode3_"),
                           _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
                       };
        }

        public static void SetString(string key, string value) =>
            dclPrefs.SetString(key, value);

        public static void SetInt(string key, int value) =>
            dclPrefs.SetInt(key, value);

        public static void SetFloat(string key, float value) =>
            dclPrefs.SetFloat(key, value);

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

        public static void DeleteAll() =>
            dclPrefs.DeleteAll();

        public static void Save() =>
            dclPrefs.Save();

        public enum Mode
        {
            Disk,
            DiskPrefix1,
            DiskPrefix2,
            DiskPrefix3,
            InMemory,
        }
    }
}
