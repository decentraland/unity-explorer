using DCL.Diagnostics;
using Plugins.DclNativeMutex;
using Plugins.DclNativeProcesses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using UnityEngine.Pool;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
#else
using Plugins.DclNativeMemoryMappedFiles;
#endif

namespace DCL.Web3.Identities
{
    /// <summary>
    /// Allows to run different profiles per each new launched instance of Explorer
    /// </summary>
    public class MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy : IPlayerPrefsIdentityProviderKeyStrategy
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        private const string MMF_NAME = "Local\\DCL_AppInstancesTracking";
        private const string MUTEX_NAME = "Local\\DCL_AppInstancesTrackingMutex";
#else
        private const string MMF_NAME = "/dcl_tracking_nmmf";
        private const string MUTEX_NAME = "/dcl_tracking";
#endif
        private const int MAX_INSTANCES_COUNT = 64;
        private const uint MUTEX_TIMEOUT_MILLISECONDS = 5000;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        private readonly MemoryMappedFile memoryMappedFile;
#else
        private readonly NamedMemoryMappedFile memoryMappedFile;
#endif

        private readonly PMutex mutex;
        private readonly int selfPID;
        private readonly ProcessName selfProcessName;

        private bool disposed;
        private string? storedKey;

        public MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            memoryMappedFile = MemoryMappedFile.CreateOrOpen(MMF_NAME, MemoryMappedFileSize(), MemoryMappedFileAccess.ReadWrite);
#else
            memoryMappedFile = new NamedMemoryMappedFile(MMF_NAME, MemoryMappedFileSize());
#endif
            mutex = new PMutex(MUTEX_NAME);
            selfPID = Process.GetCurrentProcess().Id;
            selfProcessName = new ProcessName(selfPID);
            disposed = false;
        }

        public string PlayerPrefsKey
        {
            get
            {
                if (disposed)
                    throw new ObjectDisposedException("Object is already disposed");

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

        private static void ExecuteWithAccessor(MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy instance, Action<MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy, Accessor> action)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            var viewAccessor = instance.memoryMappedFile.CreateViewAccessor(0, MemoryMappedFileSize());
            using var accessor = new Accessor(viewAccessor);
#else
            using var accessor = new Accessor(instance.memoryMappedFile);
#endif

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
            if (disposed)
            {
                ReportHub.LogWarning(
                    ReportCategory.PROFILE,
                    $"Attempt to dispose an already disposed object {nameof(MemoryMappedFilePlayerPrefsIdentityProviderKeyStrategy)}"
                );

                return;
            }

            disposed = true;
            UnregisterSelf();
            memoryMappedFile.Dispose();
            mutex.Dispose();
            selfProcessName.Dispose();
        }

        private void ReadFromFile(IList<Entry> list, Accessor accessor)
        {
            ReadFromFile<object>(list, accessor, null!, null);
        }

        private void ReadFromFile<TCtx>(IList<Entry> list, Accessor accessor, TCtx ctx, Func<Entry, TCtx, bool>? ignoreFunc)
        {
            for (int i = 0; i < MAX_INSTANCES_COUNT; i++)
            {
                Entry entry = accessor.EntryAt(i);
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

        private static void WriteToFile(IReadOnlyList<Entry> list, Accessor accessor)
        {
            // memset 0
            ZeroOutMemory(accessor);

            for (var i = 0; i < list.Count; i++)
                accessor.Write(list[i], i);
        }

        private static void ZeroOutMemory(Accessor accessor)
        {
            var nullEntry = Entry.NULL_ENTRY;
            for (var i = 0; i < MAX_INSTANCES_COUNT; i++) accessor.Write(nullEntry, i);
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

        private readonly struct Accessor : IDisposable
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            private readonly MemoryMappedViewAccessor accessor;
#else
            private readonly NamedMemoryMappedFile namedMemoryMappedFile;
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            public Accessor(MemoryMappedViewAccessor accessor)
            {
                this.accessor = accessor;
            }
#else
            public Accessor(NamedMemoryMappedFile namedMemoryMappedFile)
            {
                this.namedMemoryMappedFile = namedMemoryMappedFile;
            }
#endif

            public void Write(Entry entry, int position)
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
                int offset = Entry.StructSize * position;
                accessor.Write(offset, ref entry);
#else
                unsafe
                {
                    byte* data = (byte*)&entry;
                    Span<byte> span = new Span<byte>(data, Entry.StructSize);
                    namedMemoryMappedFile.Write(span, Entry.StructSize * position);
                }
#endif
            }

            public Entry EntryAt(int position)
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
                accessor.Read(Entry.StructSize * position, out Entry entry);
                return entry;
#else
                unsafe
                {
                    Span<byte> span = stackalloc byte[Entry.StructSize];
                    namedMemoryMappedFile.Read(Entry.StructSize * position, span);
                    return MemoryMarshal.Read<Entry>(span);
                }
#endif
            }

            public void Flush()
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
                accessor.Flush();
#else
#endif
            }

            public void Dispose()
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
                accessor.Dispose();
#endif
            }
        }
    }
}
