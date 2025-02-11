using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using UnityEngine.Pool;

namespace DCL.Web3.Identities
{
    /// <summary>
    /// Allows to run different profiles per each new launched instance of Explorer
    /// </summary>
    public class MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy : IPlayerPrefsIdentityProviderKeyStrategy
    {
        private const string MMF_NAME = "Local\\DCL_AppInstancesTracking";
        private const string MUTEX_NAME = "Local\\DCL_AppInstancesTrackingMutex";
        private const int MAX_INSTANCES_COUNT = 64;
        private const uint MUTEX_TIMEOUT_MILLISECONDS = 5000;

        private readonly MemoryMappedFile memoryMappedFile;
        private readonly PMutex mutex;
        private readonly int selfPID;
        private readonly string selfProcessName;

        private string? storedKey;

        public MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy()
        {
            memoryMappedFile = MemoryMappedFile.CreateOrOpen(MMF_NAME, MemoryMappedFileSize(), MemoryMappedFileAccess.ReadWrite);
            mutex = new PMutex(MUTEX_NAME);
            var self = Process.GetCurrentProcess();
            selfProcessName = self.ProcessName;
            selfPID = Process.GetCurrentProcess().Id;
        }

        public string PlayerPrefsKey
        {
            get
            {
                if (storedKey == null)
                    RegisterSelf();

                return storedKey!;
            }
        }

        private void RegisterSelf()
        {
            ExecuteWithAccessor(this, static (i, accessor) =>
            {
                using var _ = ListPool<Entry>.Get(out var list);

                i.ReadFromFile(list, accessor);

                if (list.Count == MAX_INSTANCES_COUNT)
                    ReportHub.LogException(
                        new Exception($"Running max amount of instances {MAX_INSTANCES_COUNT}, please close one"),
                        ReportCategory.PROFILE
                    );

                var selfEntry = Entry.NewSelf(i.selfPID, list);
                list.Add(selfEntry);
                i.storedKey = KeyFromEntry(selfEntry);

                WriteToFile(list, accessor);
            });
        }

        private void UnregisterSelf()
        {
            ExecuteWithAccessor(this, static (i, accessor) =>
            {
                using var _ = ListPool<Entry>.Get(out var list);
                i.ReadFromFile(list, accessor, i.selfPID, static (entry, selfPID) => entry.PID == selfPID);
                WriteToFile(list, accessor);
            });
        }

        private static void ExecuteWithAccessor(MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy instance, Action<MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy, MemoryMappedViewAccessor> action)
        {
            using var accessor = instance.memoryMappedFile.CreateViewAccessor(0, MemoryMappedFileSize());
            instance.mutex.WaitOne(MUTEX_TIMEOUT_MILLISECONDS);

            try
            {
                action(instance, accessor);
                accessor.Flush();
            }
            finally { instance.mutex.ReleaseMutex(); }
        }

        public void Dispose()
        {
            UnregisterSelf();
            memoryMappedFile.Dispose();
            mutex.Dispose();
        }

        private void ReadFromFile(IList<Entry> list, MemoryMappedViewAccessor accessor)
        {
            ReadFromFile<object>(list, accessor, null!, null);
        }

        private void ReadFromFile<TCtx>(IList<Entry> list, MemoryMappedViewAccessor accessor, TCtx ctx, Func<Entry, TCtx, bool>? ignoreFunc)
        {
            for (int i = 0; i < MAX_INSTANCES_COUNT; i++)
            {
                accessor.Read(Entry.StructSize * i, out Entry entry);
                if (entry.PID == 0) break;

                if (ignoreFunc != null && ignoreFunc(entry, ctx))
                    break;

                // Get process if exists
                if (TryGetProcess(entry.PID, out var p) == false)
                    continue;

                // Ensure the process is Explorer
                if (p?.ProcessName != selfProcessName)
                    continue;

                list.Add(entry);
            }
        }

        private static void WriteToFile(IReadOnlyList<Entry> list, MemoryMappedViewAccessor accessor)
        {
            // memset 0
            ZeroOutMemory(accessor);

            for (var i = 0; i < list.Count; i++)
            {
                int offset = Entry.StructSize * i;
                var entry = list[i];
                accessor.Write(offset, ref entry);
            }
        }

        private static void ZeroOutMemory(MemoryMappedViewAccessor accessor)
        {
            var nullEntry = Entry.NULL_ENTRY;

            for (int i = 0; i < MAX_INSTANCES_COUNT; i++)
            {
                int offset = Entry.StructSize * i;
                accessor.Write(offset, ref nullEntry);
            }
        }

        private static bool TryGetProcess(int pid, out Process? process)
        {
            try
            {
                process = Process.GetProcessById(pid);
                return true;
            }
            catch (Exception)
            {
                process = null;
                return false;
            }
        }

        private static string KeyFromEntry(Entry entry)
        {
            if (entry.ProfileId == 0)
                return IPlayerPrefsIdentityProviderKeyStrategy.DEFAULT_PREFS_KEY;

            return IPlayerPrefsIdentityProviderKeyStrategy.DEFAULT_PREFS_KEY + entry.ProfileId;
        }

        private static int MemoryMappedFileSize() =>
            Entry.StructSize * MAX_INSTANCES_COUNT;

        private struct Entry
        {
            public static readonly Entry NULL_ENTRY = new () { ProfileId = 0, PID = 0 };

            public int ProfileId;
            public int PID;

            public static Entry NewSelf(int pid, IReadOnlyList<Entry> entries)
            {
                using var _ = HashSetPool<int>.Get(out var set);
                foreach (Entry entry in entries) set.Add(entry.ProfileId);

                var nextFreeId = 0;

                for (int i = 0; i < entries.Count; i++)
                {
                    if (set.Contains(nextFreeId) == false)
                        break;

                    nextFreeId++;
                }

                return new ()
                {
                    PID = pid,
                    ProfileId = nextFreeId,
                };
            }

            public static int StructSize
            {
                get
                {
                    unsafe { return sizeof(Entry); }
                }
            }
        }
    }

    /// <summary>
    /// Created due named mutex is not supported by IL2CPP
    /// </summary>
    internal class PMutex : IDisposable
    {
        private const int ERROR_ALREADY_EXISTS = 183;
        /// <summary>
        /// From https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject
        /// </summary>
        private const uint WAIT_OBJECT_0 = (uint)0x00000000L;

        private readonly string name;
        private readonly IntPtr pMutex;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateMutex(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMillisecond);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReleaseMutex(IntPtr hMutex);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public PMutex(string name)
        {
            pMutex = CreateMutex(IntPtr.Zero, false, name);
            int lastError = Marshal.GetLastWin32Error();

            if (pMutex == IntPtr.Zero)
                throw new Exception("Failed to create mutex.");

            if (lastError == ERROR_ALREADY_EXISTS)
                throw new Exception("Mutex already exists, another process is using it.");
        }

        public void WaitOne(uint milliseconds)
        {
            uint result = WaitForSingleObject(pMutex, milliseconds);

            if (result != WAIT_OBJECT_0)
                throw new Exception("Cannot acquire mutex");
        }

        public void ReleaseMutex()
        {
            bool result = ReleaseMutex(pMutex);

            if (result == false)
                throw new Exception("Cannot release mutex");
        }

        public void Dispose()
        {
            CloseHandle(pMutex);
        }
    }
}
