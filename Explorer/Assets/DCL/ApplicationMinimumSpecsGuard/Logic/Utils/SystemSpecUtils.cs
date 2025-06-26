using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    /// <summary>
    ///     Contains pure, static logic functions for checking hardware specifications.
    ///     This class is separated from the profile provider to make the logic easily testable.
    /// </summary>
    public static class SystemSpecUtils
    {
        public static bool IsWindowsCpuAcceptable(string cpu)
        {
            cpu = cpu.ToLowerInvariant();

            // AMD check
            if (cpu.Contains("ryzen"))
            {
                var match = Regex.Match(cpu, @"ryzen\s*(\d)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int model))
                    return model >= 5; // Ryzen 5, 7, 9
            }

            if (cpu.Contains("threadripper"))
                return true;

            // --- \INTEL CHECK ---
            // Matches i3, i5, i7, i9 followed by a model number of 4 or 5 digits.
            // Group 1: The series (3, 5, 7, 9)
            // Group 2: The model number (e.g., 7600 or 13900)
            var intelMatch = Regex.Match(cpu, @"i([3579])-?(\d{4,5})");

            if (intelMatch.Success)
            {
                bool seriesParsed = int.TryParse(intelMatch.Groups[1].Value, out int series); // e.g., 5 from i5
                bool modelParsed = int.TryParse(intelMatch.Groups[2].Value, out int modelNumber); // e.g., 13900

                if (seriesParsed && modelParsed)
                {
                    // Condition 1: Must be i5 or higher
                    if (series < 5) return false;

                    // Condition 2: Must be 7th gen or higher.
                    // For 4-digit models (e.g., 7600), the generation is the first digit.
                    // For 5-digit models (e.g., 13900), the generation is the first two digits.
                    int generation = modelNumber / 1000; // 7600 -> 7, 13900 -> 13

                    return generation >= 7;
                }
            }

            return false;
        }

        public static bool IsWindowsGpuAcceptable(string gpu)
        {
            gpu = gpu.ToLowerInvariant();

            // NVIDIA RTX 20+ series
            if (gpu.Contains("rtx"))
            {
                var match = Regex.Match(gpu, @"rtx\s*(\d{4})");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int rtxModel))
                    return rtxModel >= 2000;
            }

            // AMD RX 5000+ series
            if (gpu.Contains("rx"))
            {
                var match = Regex.Match(gpu, @"rx\s*(\d{4})");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int rxModel))
                    return rxModel >= 5000;
            }

            // Intel Arc A500+ only (A3xx is too weak)
            if (gpu.Contains("arc"))
            {
                // Accept A5xx and A7xx series (skip weak A3xx models unless you want them)
                var match = Regex.Match(gpu, @"a(\d{3})");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int arcModel))
                    return arcModel >= 500;
            }

            return false;
        }

        public static bool ComputeShaderCheck()
        {
            return SystemInfo.supportsComputeShaders;
        }
        
        public static bool IsAppleSilicon(string deviceName)
        {
            return Regex.IsMatch(deviceName, @"apple\s+m\d", RegexOptions.IgnoreCase);
        }

        public static int TryGetMacVersionMajor(string os)
        {
            var match = Regex.Match(os, @"Mac OS X (\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        public static bool IsWindows10OrNewer(string os)
        {
            return os.Contains("Windows 10") || os.Contains("Windows 11");
        }

        public static bool IsMacOsBigSurOrNewer(string os)
        {
            if (!os.Contains("Mac OS X")) return false;

            var match = Regex.Match(os, @"(\d+)\.\d+"); // Matches "11.5", "12.0", etc.
            if (match.Success && int.TryParse(match.Groups[1].Value, out int majorVersion))
            {
                // macOS 11 (Big Sur) is the minimum.
                return majorVersion >= 11;
            }

            return false;
        }
    }
}