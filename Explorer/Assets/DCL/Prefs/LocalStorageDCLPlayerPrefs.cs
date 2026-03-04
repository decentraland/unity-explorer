#if UNITY_WEBGL
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DCL.Prefs
{
    /// <summary>
    ///     <see cref="IDCLPrefs" /> implementation for WebGL that persists all data in the browser's
    ///     <c>localStorage</c> as a single JSON blob under the key <c>dcl_prefs</c>.
    ///     Mirrors the <c>UserData</c> structure of <see cref="FileDCLPlayerPrefs" />.
    ///     <see cref="Save" /> is called automatically on every <c>Set*</c> operation so that data
    ///     is not lost if the browser tab is closed without an explicit flush.
    /// </summary>
    public class LocalStorageDCLPlayerPrefs : IDCLPrefs
    {
        private readonly UserData userData;

        public LocalStorageDCLPlayerPrefs()
        {
            userData = Load();
        }

        public void SetString(string key, string value) { DeleteKey(key); userData.Strings[key] = value; Save(); }
        public void SetInt   (string key, int value)    { DeleteKey(key); userData.Ints[key]    = value; Save(); }
        public void SetFloat (string key, float value)  { DeleteKey(key); userData.Floats[key]  = value; Save(); }
        public void SetBool  (string key, bool value)   { DeleteKey(key); userData.Bools[key]   = value; Save(); }

        public string GetString(string key, string defaultValue) => userData.Strings.GetValueOrDefault(key, defaultValue);
        public int    GetInt   (string key, int defaultValue)    => userData.Ints.GetValueOrDefault(key, defaultValue);
        public float  GetFloat (string key, float defaultValue)  => userData.Floats.GetValueOrDefault(key, defaultValue);
        public bool   GetBool  (string key, bool defaultValue)   => userData.Bools.GetValueOrDefault(key, defaultValue);

        public bool HasKey(string key) =>
            userData.Strings.ContainsKey(key) || userData.Ints.ContainsKey(key) ||
            userData.Floats.ContainsKey(key)  || userData.Bools.ContainsKey(key);

        public void DeleteKey(string key)
        {
            userData.Strings.Remove(key);
            userData.Ints.Remove(key);
            userData.Floats.Remove(key);
            userData.Bools.Remove(key);
        }

        public void DeleteAll()
        {
            userData.Strings.Clear();
            userData.Ints.Clear();
            userData.Floats.Clear();
            userData.Bools.Clear();
            Save();
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(userData);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0); // null terminator for jslib UTF8ToString

            try { DCLPrefs_Save(ptr); }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        private static UserData Load()
        {
            int bufferSize = 1024 * 64; // 64 KB — more than sufficient for prefs JSON
            IntPtr ptr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                int result = DCLPrefs_Load(ptr, bufferSize);

                if (result > 0)
                {
                    byte[] buffer = new byte[result];
                    Marshal.Copy(ptr, buffer, 0, result);
                    string json = Encoding.UTF8.GetString(buffer);
                    return JsonConvert.DeserializeObject<UserData>(json) ?? new UserData();
                }

                return new UserData();
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        [DllImport("__Internal")]
        private static extern void DCLPrefs_Save(IntPtr jsonPtr);

        [DllImport("__Internal")]
        private static extern int DCLPrefs_Load(IntPtr resultPtr, int resultSize);

        [Serializable]
        private class UserData
        {
            public Dictionary<string, string> Strings { get; set; } = new ();
            public Dictionary<string, int>    Ints    { get; set; } = new ();
            public Dictionary<string, float>  Floats  { get; set; } = new ();
            public Dictionary<string, bool>   Bools   { get; set; } = new ();
        }
    }
}
#endif
