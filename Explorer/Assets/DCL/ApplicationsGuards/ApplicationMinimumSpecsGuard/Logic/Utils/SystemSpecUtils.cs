using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

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
        };

        private static readonly string[] INTEGRATED_GPU_KEYWORDS =
        {
            "intel(r) hd graphics", "intel(r) uhd graphics", "intel iris", "iris(r) xe graphics", "amd radeon(tm) graphics", // Catches the case from your image
            "amd radeon graphics", "amd radeon vega", "amd radeon r5", // Catches R-series like R5, R6, R7
            "amd radeon r6", "amd radeon r7"
        };

        private const int MIN_MACOS_MAJOR_VERSION = 11;
        private const string MACOS_VERSION_PATTERN = @"(\d+)\.\d+";

        // CPU Requirement Constants
        // Keywords and Patterns
        private const string RYZEN_CPU_PATTERN = @"ryzen\s*(\d)";
        private const string INTEL_CPU_PATTERN = @"i([3579])-?(\d{4,5})";
        private const string INTEL_ULTRA_CPU_PATTERN = @"ultra\s+([579])";

        private static readonly string[] ALWAYS_ACCEPTED_CPU_KEYWORDS =
        {
            "threadripper"
        };

        // Numeric Thresholds
        private const int MIN_RYZEN_SERIES = 5;
        private const int MIN_INTEL_SERIES = 5;
        private const int MIN_INTEL_GENERATION = 7;
        private const int MIN_INTEL_ULTRA_SERIES = 5;

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

            var intelUltraMatch = Regex.Match(cpu, INTEL_ULTRA_CPU_PATTERN);
            if (intelUltraMatch.Success && int.TryParse(intelUltraMatch.Groups[1].Value, out int ultraSeries))
                return ultraSeries >= MIN_INTEL_ULTRA_SERIES;
            
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

        public static bool IsIntegratedGpu(string gpuName)
        {
            if (string.IsNullOrEmpty(gpuName))
                return false;

            string lowerGpuName = gpuName.ToLowerInvariant();

            foreach (string keyword in INTEGRATED_GPU_KEYWORDS)
            {
                if (lowerGpuName.Contains(keyword))
                    return true;
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
        
        public static bool IsDirectX12Compatible()
        {
            // Check current graphics API
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
                return true;

            // Check if DX12 is available but not currently active
            // This requires checking supported graphics APIs
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return SystemInfo.graphicsDeviceVersion.Contains("Direct3D 12") ||
                   SystemInfo.graphicsDeviceVersion.Contains("D3D12");
#else
                return false;
#endif
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
        
        /// <summary>
        ///     Checks if the provided memory size meets the minimum requirement,
        ///     accounting for reporting discrepancies (e.g., 15.9 GB for a 16 GB module).
        ///     This method is suitable for both System RAM and GPU VRAM.
        /// </summary>
        /// <param name="actualMemoryMB">The memory size reported by the system in Megabytes.</param>
        /// <param name="requiredMemoryMB">The minimum required memory size in Megabytes (e.g., 16384 for 16 GB).</param>
        /// <returns>True if the effective memory size meets the requirement.</returns>
        public static bool IsMemorySizeSufficient(int actualMemoryMB, int requiredMemoryMB)
        {
            // To handle cases where hardware is marketed in decimal Gigabytes (GB)
            // but reported by the OS in binary Megabytes (MB), we can't do a direct comparison.
            // A 16 GB module often reports as ~16280 MB, which is less than the binary 16 GiB (16384 MB).
            //
            // The solution is to compare values in their effective "advertised" Gigabyte size.

            // 1. Convert requirement to whole GB (e.g., 16384 -> 16).
            int requiredGB = requiredMemoryMB / 1024;

            // 2. Convert actual MB to a floating-point GB value (e.g., 16280 -> 15.9).
            float actualGBFloat = actualMemoryMB / 1024f;

            // 3. Round to the nearest whole number (e.g., 15.9 -> 16).
            // This correctly identifies the "advertised" size of the hardware.
            int roundedActualGB = Mathf.RoundToInt(actualGBFloat);

            // 4. Compare the effective (rounded) GB against the required GB.
            return roundedActualGB >= requiredGB;
        }
    }
}