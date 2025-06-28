using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    /// <summary>
    ///     Contains pure, static logic functions for checking hardware specifications.
    ///     All requirements are defined as constants and arrays at the top for easy editing.
    /// </summary>
    public static class SystemSpecUtils
    {
        // Platform Requirements
        private static readonly string[] ACCEPTABLE_WINDOWS_VERSIONS =
        {
            "Windows 10", "Windows 11"
        };

        private static readonly string[] MACOS_IDENTIFIER_KEYWORDS =
        {
            "Mac OS X", "macOS"
        }; // More future-proof

        private const int MIN_MACOS_MAJOR_VERSION = 11; // Big Sur
        private const string MACOS_VERSION_PATTERN = @"(\d+)\.\d+";

        // CPU Requirement Constants
        // Keywords and Patterns
        private const string RYZEN_CPU_PATTERN = @"ryzen\s*(\d)";
        private const string INTEL_CPU_PATTERN = @"i([3579])-?(\d{4,5})";

        private static readonly string[] ALWAYS_ACCEPTED_CPU_KEYWORDS =
        {
            "threadripper"
        };

        // Numeric Thresholds
        private const int MIN_RYZEN_SERIES = 5;
        private const int MIN_INTEL_SERIES = 5;
        private const int MIN_INTEL_GENERATION = 7;

        // GPU Requirement Constants
        // Keywords and Patterns
        private const string RTX_GPU_PATTERN = @"rtx\s*(\d{4})";
        private const string RX_GPU_PATTERN = @"rx\s*(\d{4})";
        private const string ARC_GPU_PATTERN = @"a(\d{3})";

        // Numeric Thresholds
        private const int MIN_RTX_SERIES = 2000;
        private const int MIN_RX_SERIES = 5000;
        private const int MIN_ARC_SERIES = 500;

        // Mac Silicon Requirement Constants
        private const string APPLE_SILICON_PATTERN = @"apple\s+m\d";
        

        public static bool IsWindowsCpuAcceptable(string cpu)
        {
            cpu = cpu.ToLowerInvariant();

            foreach (string keyword in ALWAYS_ACCEPTED_CPU_KEYWORDS)
            {
                if (cpu.Contains(keyword))
                    return true;
            }

            var ryzenMatch = Regex.Match(cpu, RYZEN_CPU_PATTERN);
            if (ryzenMatch.Success && int.TryParse(ryzenMatch.Groups[1].Value, out int model))
                return model >= MIN_RYZEN_SERIES;

            var intelMatch = Regex.Match(cpu, INTEL_CPU_PATTERN);
            if (intelMatch.Success)
            {
                if (int.TryParse(intelMatch.Groups[1].Value, out int series) &&
                    int.TryParse(intelMatch.Groups[2].Value, out int modelNumber))
                {
                    if (series < MIN_INTEL_SERIES) return false;
                    int generation = modelNumber / 1000;
                    return generation >= MIN_INTEL_GENERATION;
                }
            }
            return false;
        }

        public static bool IsWindowsGpuAcceptable(string gpu)
        {
            gpu = gpu.ToLowerInvariant();

            var rtxMatch = Regex.Match(gpu, RTX_GPU_PATTERN);
            if (rtxMatch.Success && int.TryParse(rtxMatch.Groups[1].Value, out int rtxModel))
                return rtxModel >= MIN_RTX_SERIES;

            var rxMatch = Regex.Match(gpu, RX_GPU_PATTERN);
            if (rxMatch.Success && int.TryParse(rxMatch.Groups[1].Value, out int rxModel))
                return rxModel >= MIN_RX_SERIES;

            var arcMatch = Regex.Match(gpu, ARC_GPU_PATTERN);
            if (arcMatch.Success && int.TryParse(arcMatch.Groups[1].Value, out int arcModel))
                return arcModel >= MIN_ARC_SERIES;

            return false;
        }

        public static bool IsWindowsVersionAcceptable(string os)
        {
            foreach (string version in ACCEPTABLE_WINDOWS_VERSIONS)
            {
                if (os.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static bool IsMacOSVersionAcceptable(string os)
        {
            bool isMac = false;
            foreach (string keyword in MACOS_IDENTIFIER_KEYWORDS)
            {
                if (os.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isMac = true;
                    break; // Exit the loop as soon as we find a match
                }
            }

            if (!isMac)
                return false;

            var match = Regex.Match(os, MACOS_VERSION_PATTERN);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int majorVersion))
            {
                return majorVersion >= MIN_MACOS_MAJOR_VERSION;
            }

            return false;
        }

        public static bool IsAppleSilicon(string deviceName)
        {
            return Regex.IsMatch(deviceName, APPLE_SILICON_PATTERN, RegexOptions.IgnoreCase);
        }
        
        public static bool ComputeShaderCheck()
        {
            return SystemInfo.supportsComputeShaders;
        }
    }
}