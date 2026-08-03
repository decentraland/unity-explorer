using System;
using System.Collections.Generic;

namespace UUAV
{
    /// <summary>
    /// Read-only diagnostics surface for external debug UIs: a snapshot of
    /// the runtime state plus a ring of the most recent native error and
    /// warning lines. Safe to call whether or not the runtime is initialized,
    /// and degrades gracefully when the native library (or a fresh-enough
    /// build of it) is missing.
    /// </summary>
    public static class UUAVDebug
    {
        // The source of truth is the rust counterpart: Lifecycle in
        // uuav-client/src/connection.rs, surfaced by uuav_lifecycle. Only
        // Unavailable is C#-side: an uninitialized runtime, a stale binary
        // without the export, or a missing library.
        public enum Lifecycle
        {
            Unavailable = -1,
            Running = 0,
            Recovering = 1,
            Failed = 2,
            ShutDown = 3,
        }

        public readonly struct Info
        {
            public readonly bool NativeLibLoaded;
            public readonly bool Initialized;
            public readonly ulong PlayersCount;
            public readonly Lifecycle Lifecycle;
            public readonly string AbiVersion;
            public readonly string? DeviceRemoveReason;

            public Info(
                bool nativeLibLoaded,
                bool initialized,
                ulong playersCount,
                Lifecycle lifecycle,
                string abiVersion,
                string? deviceRemoveReason
            )
            {
                NativeLibLoaded = nativeLibLoaded;
                Initialized = initialized;
                PlayersCount = playersCount;
                Lifecycle = lifecycle;
                AbiVersion = abiVersion;
                DeviceRemoveReason = deviceRemoveReason;
            }
        }

        private const int RecentCapacity = 10;

        // pushed from native playback threads (UUAVRuntime callbacks), read
        // from the main thread: the lock covers both sides
        private static readonly object recentGate = new object();
        private static readonly string[] recent = new string[RecentCapacity];
        private static int recentHead;
        private static int recentCount;

        // the native side returns a pointer into static storage; constant per
        // loaded binary
        private static string? cachedAbiVersion;

        public static Info Query()
        {
            Status status;
            try
            {
                status = NativeMethods.uuav_status();
            }
            catch (DllNotFoundException)
            {
                return new Info(
                    nativeLibLoaded: false,
                    initialized: false,
                    playersCount: 0,
                    Lifecycle.Unavailable,
                    abiVersion: "unavailable",
                    deviceRemoveReason: null
                );
            }

            return new Info(
                nativeLibLoaded: true,
                status.Initialized,
                status.PlayersCount,
                QueryLifecycle(),
                AbiVersion(),
                status.ConsumeDeviceRemoveReason()
            );
        }

        /// <summary>
        /// Latest native error/warning lines, oldest first. Clears and refills
        /// <paramref name="target"/>.
        /// </summary>
        public static void CopyRecentMessages(List<string> target)
        {
            target.Clear();
            lock (recentGate)
            {
                for (var i = 0; i < recentCount; i++)
                {
                    target.Add(recent[(recentHead - recentCount + i + RecentCapacity) % RecentCapacity]);
                }
            }
        }

        // called from any native playback thread; public so host debug tooling
        // and tests can exercise the ring - it only feeds this debug surface
        public static void Push(string message)
        {
            lock (recentGate)
            {
                recent[recentHead] = message;
                recentHead = (recentHead + 1) % RecentCapacity;
                if (recentCount < RecentCapacity)
                {
                    recentCount++;
                }
            }
        }

        private static Lifecycle QueryLifecycle()
        {
            int raw;
            try
            {
                raw = NativeMethods.uuav_lifecycle();
            }
            catch (EntryPointNotFoundException)
            {
                // binary predates the export
                return Lifecycle.Unavailable;
            }

            return raw >= (int)Lifecycle.Running && raw <= (int)Lifecycle.ShutDown
                ? (Lifecycle)raw
                : Lifecycle.Unavailable;
        }

        private static string AbiVersion()
        {
            return cachedAbiVersion ??= Utf8.PtrToString(NativeMethods.uuav_abi_version()) ?? "unknown";
        }
    }
}
