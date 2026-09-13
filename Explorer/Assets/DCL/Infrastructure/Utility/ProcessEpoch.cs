using System;
using System.Globalization;
using System.IO;

namespace Utility
{
    /// <summary>
    ///     The instant the OS started this process — the one epoch every real-time gate that must order
    ///     exactly across assemblies measures from, whatever order their statics load in. Resolution never
    ///     throws, so a static initializer may read it on any thread and under any scripting backend.
    /// </summary>
    public static class ProcessEpoch
    {
        private const string LINUX_SELF_STAT = "/proc/self/stat";
        private const string LINUX_STAT = "/proc/stat";
        private const string LINUX_BOOT_TIME_KEY = "btime ";

        // Position of starttime (field 22 of /proc/self/stat) once the "(comm)" column is consumed
        private const int LINUX_START_TIME_FIELD = 19;

        // /proc reports starttime in USER_HZ ticks, fixed at 100 whatever the kernel's HZ is
        private const double LINUX_USER_HZ = 100.0;

        public static DateTime StartUtc { get; } = Resolve();

        private static DateTime Resolve()
        {
            if (TryReadLinuxProcStart(out DateTime linuxStart))
                return linuxStart;

#if ENABLE_IL2CPP
            // IL2CPP exposes no process-start query beyond /proc; one first-touch stamp still gives every gate the same epoch
            return DateTime.UtcNow;
#else
            return System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
#endif
        }

        private static bool TryReadLinuxProcStart(out DateTime startUtc)
        {
            startUtc = default(DateTime);

            if (!File.Exists(LINUX_SELF_STAT) || !File.Exists(LINUX_STAT))
                return false;

            string selfStat;
            string stat;

            try
            {
                selfStat = File.ReadAllText(LINUX_SELF_STAT);
                stat = File.ReadAllText(LINUX_STAT);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }

            return TryParseLinuxProcStart(selfStat, stat, out startUtc);
        }

        /// <summary>
        ///     Combines the boot instant from <c>/proc/stat</c> (<c>btime</c>, Unix seconds) with the process
        ///     start offset from <c>/proc/self/stat</c> (<c>starttime</c>, USER_HZ ticks since boot).
        /// </summary>
        public static bool TryParseLinuxProcStart(string selfStat, string stat, out DateTime startUtc)
        {
            startUtc = default(DateTime);

            // comm may contain spaces and parentheses; the fixed-position columns start after its closing one
            int commEnd = selfStat.LastIndexOf(')');

            if (commEnd < 0)
                return false;

            string[] fields = selfStat.Substring(commEnd + 1).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length <= LINUX_START_TIME_FIELD)
                return false;

            if (!long.TryParse(fields[LINUX_START_TIME_FIELD], NumberStyles.None, CultureInfo.InvariantCulture, out long startTicks))
                return false;

            if (!TryReadBootSeconds(stat, out long bootSeconds))
                return false;

            startUtc = DateTime.UnixEpoch.AddSeconds(bootSeconds + (startTicks / LINUX_USER_HZ));
            return true;
        }

        private static bool TryReadBootSeconds(string stat, out long bootSeconds)
        {
            bootSeconds = 0;
            int keyAt = stat.IndexOf(LINUX_BOOT_TIME_KEY, StringComparison.Ordinal);

            while (keyAt > 0 && stat[keyAt - 1] != '\n')
                keyAt = stat.IndexOf(LINUX_BOOT_TIME_KEY, keyAt + 1, StringComparison.Ordinal);

            if (keyAt < 0)
                return false;

            int valueStart = keyAt + LINUX_BOOT_TIME_KEY.Length;
            int valueEnd = stat.IndexOf('\n', valueStart);

            if (valueEnd < 0)
                valueEnd = stat.Length;

            return long.TryParse(stat.Substring(valueStart, valueEnd - valueStart).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out bootSeconds);
        }
    }
}
