using System;
using System.Diagnostics;

namespace DCL.SDKComponents.Tween.Playground
{
    public static class NtpUtils
    {
        private const ulong SECONDS_FROM1900_TO1970 = 2_208_988_800UL; // 70 years + 17 leap‑days
        private const double FRAC_TO_MS = 1000.0 / 0x1_0000_0000UL;

        private static readonly double MS_PER_TICK = 1000.0 / Stopwatch.Frequency;

        /// <summary>
        /// Convert <see cref="Stopwatch.GetTimestamp"/> ticks → milliseconds.
        /// </summary>
        public static double StopwatchTicksToMilliseconds(long ticks) => ticks * MS_PER_TICK;

        /// <summary>
        /// Creates a minimal NTP request buffer. RFC 5905 §7.3
        /// </summary>
        public static byte[] CreateNtpRequestBuffer()
        {
            byte[] ntpData = new byte[48];
            const byte li = 0,
                       vn = 3, // protocol version
                       mode = 3; // client mode

            ntpData[0] = (li << 6) | (vn << 3) | mode;
            return ntpData;
        }

        /// <summary>
        /// Convert 64‑bit NTP timestamp → Unix‑epoch milliseconds.
        /// </summary>
        public static double NtpEpochToUnixMilliseconds(ulong ntpTimestamp)
        {
            ulong seconds  = ntpTimestamp >> 32;
            ulong fraction = ntpTimestamp & 0xFFFF_FFFFUL;
            return ((seconds - SECONDS_FROM1900_TO1970) * 1000.0) + (fraction * FRAC_TO_MS);
        }

        /// <summary>
        /// Convert "Unix epoch" milliseconds → 64‑bit NTP timestamp.
        /// </summary>
        public static ulong UnixMillisecondsToNtpTimestamp(double unixMs)
        {
            ulong seconds  = (ulong)(unixMs / 1000.0) + SECONDS_FROM1900_TO1970;
            ulong fraction = (ulong)((unixMs % 1000.0) / 1000.0 * 0x1_0000_0000UL);
            return (seconds << 32) | fraction;
        }

        /// <summary>
        /// Write a 64‑bit NTP timestamp into <paramref name="buf"/> at <paramref name="ofs"/> (big‑endian).
        /// </summary>
        public static void WriteTimestamp(byte[] buf, int ofs, ulong ntpTimestamp)
        {
            buf[ofs + 0] = (byte)(ntpTimestamp >> 56);
            buf[ofs + 1] = (byte)(ntpTimestamp >> 48);
            buf[ofs + 2] = (byte)(ntpTimestamp >> 40);
            buf[ofs + 3] = (byte)(ntpTimestamp >> 32);
            buf[ofs + 4] = (byte)(ntpTimestamp >> 24);
            buf[ofs + 5] = (byte)(ntpTimestamp >> 16);
            buf[ofs + 6] = (byte)(ntpTimestamp >>  8);
            buf[ofs + 7] = (byte)(ntpTimestamp >>  0);
        }

        /// <summary>
        /// NTP timestamp parsing & conversion
        /// 64‑bit unsigned, big‑endian: first 32 bits = seconds, next 32 bits = fraction
        /// </summary>
        public static ulong ReadTimestamp(byte[] buf, int ofs)
        {
            ulong intPart = ((ulong)buf[ofs + 0] << 24) |
                            ((ulong)buf[ofs + 1] << 16) |
                            ((ulong)buf[ofs + 2] <<  8) |
                            (ulong)buf[ofs + 3];

            ulong fracPart = ((ulong)buf[ofs + 4] << 24) |
                             ((ulong)buf[ofs + 5] << 16) |
                             ((ulong)buf[ofs + 6] <<  8) |
                             (ulong)buf[ofs + 7];

            return (intPart << 32) | fracPart;
        }
    }
}
