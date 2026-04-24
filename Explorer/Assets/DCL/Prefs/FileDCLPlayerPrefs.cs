using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.Prefs
{
    public class FileDCLPlayerPrefs : IDCLPrefs, IDisposable
    {
        private const int CONCURRENT_CLIENTS = 16;
        private const string PREFS_FILENAME = "userdata_{0}.json";
        private const string CLAIM_FILENAME = "userdata_{0}.claim";

        // Grace period before an empty claim file (e.g. crashed mid-claim) is treated as stale.
        private static readonly TimeSpan STALE_EMPTY_CLAIM_AGE = TimeSpan.FromSeconds(30);

        private readonly FileStream fileStream;
        private readonly FileStream claimStream;
        private readonly string claimPath;
        private readonly UserData userData;

        private readonly UnityDCLPlayerPrefs unityPrefs;

        private bool dataChanged;
        private bool disposed;

        public static int PrefsInstanceNumber { get; private set; }

        public FileDCLPlayerPrefs() : this(Application.persistentDataPath) { }

        internal FileDCLPlayerPrefs(string baseDir)
        {
            PrefsInstanceNumber = -1;

            SlotClaim selfClaim = BuildSelfClaim();

            for (var i = 0; i < CONCURRENT_CLIENTS; i++)
            {
                string dataPath = Path.Combine(baseDir, string.Format(PREFS_FILENAME, i));
                string currentClaimPath = Path.Combine(baseDir, string.Format(CLAIM_FILENAME, i));

                if (!TryClaim(currentClaimPath, selfClaim, out FileStream acquiredClaim))
                    continue;

                try
                {
                    fileStream = File.Open(dataPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                }
                catch (IOException)
                {
                    try { acquiredClaim.Dispose(); } catch { /* ignored */ }
                    try { File.Delete(currentClaimPath); } catch { /* ignored */ }
                    continue;
                }

                claimStream = acquiredClaim;
                claimPath = currentClaimPath;
                PrefsInstanceNumber = i;

                // Migrate legacy Unity PlayerPrefs only on the primary instance.
                if (i == 0)
                    unityPrefs = new UnityDCLPlayerPrefs();

                break;
            }

            if (fileStream == null)
                throw new Exception("Failed to open unityPrefs file");

            using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 1024, true);
            string json = reader.ReadToEnd();
            userData = JsonConvert.DeserializeObject<UserData>(json) ?? new UserData();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            try { SaveSync(); } catch { /* ignored */ }
            try { fileStream?.Dispose(); } catch { /* ignored */ }
            try { claimStream?.Dispose(); } catch { /* ignored */ }

            if (!string.IsNullOrEmpty(claimPath))
            {
                try { File.Delete(claimPath); } catch { /* ignored */ }
            }
        }

        public void SetString(string key, string value)
        {
            if (userData.Strings.TryGetValue(key, out string existing) && existing == value) return;

            lock (userData)
            {
                userData.Strings[key] = value;
                dataChanged = true;
            }
        }

        public void SetInt(string key, int value)
        {
            if (userData.Ints.TryGetValue(key, out int existing) && existing == value) return;

            lock (userData)
            {
                userData.Ints[key] = value;
                dataChanged = true;
            }
        }

        public void SetFloat(string key, float value)
        {
            if (userData.Floats.TryGetValue(key, out float existing) && Mathf.Approximately(existing, value)) return;

            lock (userData)
            {
                userData.Floats[key] = value;
                dataChanged = true;
            }
        }

        public void SetBool(string key, bool value)
        {
            if (userData.Bools.TryGetValue(key, out bool existing) && existing == value) return;

            lock (userData)
            {
                userData.Bools[key] = value;
                dataChanged = true;
            }
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

        public bool GetBool(string key, bool defaultValue)
        {
            MigrateBool(key);
            return userData.Bools.GetValueOrDefault(key, defaultValue);
        }

        public bool HasKey(string key) =>
            (unityPrefs != null && unityPrefs.HasKey(key)) || userData.Strings.ContainsKey(key) || userData.Ints.ContainsKey(key) || userData.Floats.ContainsKey(key) || userData.Bools.ContainsKey(key);

        public void DeleteKey(string key)
        {
            if (!HasKey(key)) return;

            lock (userData)
            {
                userData.Strings.Remove(key);
                userData.Ints.Remove(key);
                userData.Floats.Remove(key);
                userData.Bools.Remove(key);
                PlayerPrefs.DeleteKey(key);
                dataChanged = true;
            }
        }

        public void DeleteAll()
        {
            lock (userData)
            {
                userData.Strings.Clear();
                userData.Ints.Clear();
                userData.Floats.Clear();
                userData.Bools.Clear();
                PlayerPrefs.DeleteAll();
                dataChanged = true;
            }
        }

        public void Save()
        {
            if (!dataChanged) return;

            dataChanged = false;

            // Run save on a background thread
            Task.Run(WriteToDisk);
        }

        public void SaveSync()
        {
            if (!dataChanged) return;

            dataChanged = false;

            WriteToDisk();
        }

        private void WriteToDisk()
        {
            lock (fileStream)
            lock (userData)
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                fileStream.SetLength(0);

                using var writer = new StreamWriter(fileStream, Encoding.UTF8, 1024, true);
                string json = JsonConvert.SerializeObject(userData);
                writer.Write(json);
                writer.Flush();
            }
        }

        private void MigrateString(string key)
        {
            if (unityPrefs == null || !unityPrefs.HasKey(key)) return;

            lock (userData)
            {
                userData.Strings.TryAdd(key, unityPrefs!.GetString(key, null));
                dataChanged = true;
            }

            unityPrefs.DeleteKey(key);
            unityPrefs.Save();
        }

        private void MigrateInt(string key)
        {
            if (unityPrefs == null || !unityPrefs.HasKey(key)) return;

            lock (userData)
            {
                userData.Ints.TryAdd(key, unityPrefs!.GetInt(key, 0));
                dataChanged = true;
            }

            unityPrefs.DeleteKey(key);
            unityPrefs.Save();
        }

        private void MigrateFloat(string key)
        {
            if (unityPrefs == null || !unityPrefs.HasKey(key)) return;

            lock (userData)
            {
                userData.Floats.TryAdd(key, unityPrefs!.GetFloat(key, 0f));
                dataChanged = true;
            }

            unityPrefs.DeleteKey(key);
            unityPrefs.Save();
        }

        private void MigrateBool(string key)
        {
            if (unityPrefs == null || !unityPrefs.HasKey(key)) return;

            lock (userData)
            {
                userData.Bools.TryAdd(key, unityPrefs!.GetBool(key, false));
                dataChanged = true;
            }

            unityPrefs.DeleteKey(key);
            unityPrefs.Save();
        }

        // Claim-file slot ownership: FileMode.CreateNew is kernel-atomic on both Windows (CREATE_NEW)
        // and POSIX (O_CREAT|O_EXCL), so exactly one concurrent caller wins the race. Stale claims
        // (owner PID no longer alive or running a different executable) are reclaimable.
        private static bool TryClaim(string path, SlotClaim selfClaim, out FileStream stream)
        {
            stream = null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                }
                catch (IOException)
                {
                    if (!IsClaimStale(path)) return false;

                    try { File.Delete(path); }
                    catch (IOException) { return false; }
                    continue;
                }

                try
                {
                    byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(selfClaim));
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }
                catch
                {
                    try { stream.Dispose(); } catch { /* ignored */ }
                    try { File.Delete(path); } catch { /* ignored */ }
                    stream = null;
                    return false;
                }

                return true;
            }

            return false;
        }

        private static bool IsClaimStale(string path)
        {
            string json;
            try { json = File.ReadAllText(path); }
            catch (IOException) { return false; }   // unreadable (e.g. owner holds FileShare.None on Windows) — treat as live
            catch { return true; }

            if (string.IsNullOrWhiteSpace(json))
            {
                // Claim file exists but is empty. Either a peer is mid-claim (narrow window between CreateNew
                // and PID write), or a previous owner crashed inside that window. Fall back to file age.
                try
                {
                    TimeSpan age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
                    return age > STALE_EMPTY_CLAIM_AGE;
                }
                catch { return false; }
            }

            SlotClaim claim;
            try { claim = JsonConvert.DeserializeObject<SlotClaim>(json); }
            catch { return true; }

            return !IsClaimLive(claim);
        }

        private static bool IsClaimLive(SlotClaim claim)
        {
            if (claim.Pid <= 0) return false;

            Process process;
            try { process = Process.GetProcessById(claim.Pid); }
            catch (ArgumentException) { return false; }
            catch { return true; }

            try
            {
                using (process)
                {
                    if (process.HasExited) return false;

                    // Guard against PID reuse by a different process.
                    if (!string.IsNullOrEmpty(claim.ProcessName) &&
                        !string.Equals(process.ProcessName, claim.ProcessName, StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (!string.IsNullOrEmpty(claim.MainModulePath))
                    {
                        try
                        {
                            string livePath = process.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(livePath))
                                return string.Equals(livePath, claim.MainModulePath, StringComparison.OrdinalIgnoreCase);
                        }
                        catch { /* fall through to name-only match */ }
                    }

                    return true;
                }
            }
            catch { return true; }
        }

        private static SlotClaim BuildSelfClaim()
        {
            var claim = new SlotClaim { ProcessName = string.Empty, MainModulePath = string.Empty };

            try
            {
                using Process self = Process.GetCurrentProcess();
                claim.Pid = self.Id;

                try { claim.ProcessName = self.ProcessName ?? string.Empty; }
                catch { /* ignored */ }

                try { claim.MainModulePath = self.MainModule?.FileName ?? string.Empty; }
                catch { /* MainModule can throw on macOS without entitlements */ }
            }
            catch
            {
                // If we can't read our own PID, leave the claim with Pid=0 which other processes will treat as stale.
            }

            return claim;
        }

        [Serializable]
        internal struct SlotClaim
        {
            public int Pid;
            public string ProcessName;
            public string MainModulePath;
        }

        [Serializable]
        private class UserData
        {
            public Dictionary<string, string> Strings { get; private set; } = new ();
            public Dictionary<string, int> Ints { get; private set; } = new ();
            public Dictionary<string, float> Floats { get; private set; } = new ();

            public Dictionary<string, bool> Bools { get; private set; } = new ();
        }
    }
}
