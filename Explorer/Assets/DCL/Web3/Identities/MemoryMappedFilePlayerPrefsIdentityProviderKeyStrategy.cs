using DCL.Diagnostics;
using Plugins.DclNativeMutex;
using Plugins.DclNativeProcesses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using UnityEngine.Pool;

namespace DCL.Web3.Identities
{
    /// <summary>
    /// Allows to run different profiles per each new launched instance of Explorer
    /// </summary>
    public class MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy : IPlayerPrefsIdentityProviderKeyStrategy
    {
        private const string MMF_NAME = "Local\\DCL_AppInstancesTracking";
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        private const string MUTEX_NAME = "Local\\DCL_AppInstancesTrackingMutex";
#else
        private const string MUTEX_NAME = "/dcl_tracking";
#endif
        private const int MAX_INSTANCES_COUNT = 64;
        private const uint MUTEX_TIMEOUT_MILLISECONDS = 5000;

        private readonly MemoryMappedFile memoryMappedFile;
        private readonly PMutex mutex;
        private readonly int selfPID;
        private readonly ProcessName selfProcessName;

        private string? storedKey;

        public MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy()
        {
            memoryMappedFile = MemoryMappedFile.CreateOrOpen(MMF_NAME, MemoryMappedFileSize(), MemoryMappedFileAccess.ReadWrite);
            mutex = new PMutex(MUTEX_NAME);
            selfPID = Process.GetCurrentProcess().Id;
            selfProcessName = new ProcessName(selfPID);
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
            selfProcessName.Dispose();
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

                using var processName = new ProcessName(entry.PID);

                if (processName.IsEmpty)
                    continue;

                // Ensure the process is Explorer
                if (processName.Name != selfProcessName.Name)
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
}
