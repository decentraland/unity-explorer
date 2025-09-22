using DCL.FeatureFlags;
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
        private const string FEATURE_FLAG_VARIANT = "minimum_requirements";

        public static bool IsWindowsCpuAcceptable(string cpu)
        {
            FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.MINIMUM_REQUIREMENTS, FEATURE_FLAG_VARIANT, out MinimumRequirementsDefinition minimumRequirements);

            cpu = cpu.ToLowerInvariant();

            foreach (string keyword in minimumRequirements.always_accepted_cpus)
            {
                if (cpu.Contains(keyword))
                    return true;
            }

            var ryzenMatch = Regex.Match(cpu, minimumRequirements.ryzen_supported_cpu_regex);
            if (ryzenMatch.Success && int.TryParse(ryzenMatch.Groups[1].Value, out int model))
                return model >= minimumRequirements.ryzen_supported_minimum_series;

            var intelUltraMatch = Regex.Match(cpu, minimumRequirements.intel_ultra_cpu_supported_version_regex);
            if (intelUltraMatch.Success && int.TryParse(intelUltraMatch.Groups[1].Value, out int ultraSeries))
                return ultraSeries >= minimumRequirements.intel_ultra_supported_minimum_generation;

            var intelMatch = Regex.Match(cpu, minimumRequirements.intel_cpu_supported_version_regex);
            if (intelMatch.Success)
            {
                if (int.TryParse(intelMatch.Groups[1].Value, out int series) &&
                    int.TryParse(intelMatch.Groups[2].Value, out int modelNumber))
                {
                    if (series < minimumRequirements.intel_supported_minimum_series) return false;
                    int generation = modelNumber / 1000;
                    return generation >= minimumRequirements.intel_supported_minimum_generation;
                }
            }
            return false;
        }

        public static bool IsIntegratedGpu(string gpuName)
        {
            if (string.IsNullOrEmpty(gpuName))
                return false;

            string lowerGpuName = gpuName.ToLowerInvariant();

            FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.MINIMUM_REQUIREMENTS, FEATURE_FLAG_VARIANT, out MinimumRequirementsDefinition minimumRequirements);
            foreach (string keyword in minimumRequirements.integrated_gpu_supported_versions)
            {
                if (lowerGpuName.Contains(keyword))
                    return true;
            }

            return false;
        }

        public static bool IsWindowsGpuAcceptable(string gpu)
        {
            FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.MINIMUM_REQUIREMENTS, FEATURE_FLAG_VARIANT, out MinimumRequirementsDefinition minimumRequirements);

            gpu = gpu.ToLowerInvariant();

            var rtxMatch = Regex.Match(gpu, minimumRequirements.rtx_gpu_supported_version_regex);
            if (rtxMatch.Success && int.TryParse(rtxMatch.Groups[1].Value, out int rtxModel))
                return rtxModel >= minimumRequirements.minimum_rtx_supported_version;

            var rxMatch = Regex.Match(gpu, minimumRequirements.rx_gpu_supported_version_regex);
            if (rxMatch.Success && int.TryParse(rxMatch.Groups[1].Value, out int rxModel))
                return rxModel >= minimumRequirements.minimum_rx_supported_version;

            var arcMatch = Regex.Match(gpu, minimumRequirements.arc_gpu_supported_version_regex);
            if (arcMatch.Success && int.TryParse(arcMatch.Groups[1].Value, out int arcModel))
                return arcModel >= minimumRequirements.minimum_arc_supported_version;

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
            FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.MINIMUM_REQUIREMENTS, FEATURE_FLAG_VARIANT, out MinimumRequirementsDefinition minimumRequirements);
            foreach (string version in minimumRequirements.windows_supported_versions)
            {
                if (os.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static bool IsMacOSVersionAcceptable(string os)
        {
            bool isMac = false;
            FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.MINIMUM_REQUIREMENTS, FEATURE_FLAG_VARIANT, out MinimumRequirementsDefinition minimumRequirements);
            foreach (string keyword in minimumRequirements.mac_supported_versions)
            {
                if (os.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isMac = true;
                    break; // Exit the loop as soon as we find a match
                }
            }

            if (!isMac)
                return false;

            var match = Regex.Match(os, minimumRequirements.macos_supported_version_regex);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int majorVersion))
            {
                return majorVersion >= minimumRequirements.minimum_macos_major_version;
            }

            return false;
        }

        public static bool IsAppleSilicon(string deviceName)
        {
            FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.MINIMUM_REQUIREMENTS, FEATURE_FLAG_VARIANT, out MinimumRequirementsDefinition minimumRequirements);

            return Regex.IsMatch(deviceName, minimumRequirements.apple_silicon_supported_regex, RegexOptions.IgnoreCase);
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

        [Serializable]
        private struct MinimumRequirementsDefinition
        {
            public string[] windows_supported_versions;
            public string[] mac_supported_versions;
            public string[] integrated_gpu_supported_versions;
            public string[] always_accepted_cpus;
            public int minimum_macos_major_version;
            public string macos_supported_version_regex;
            public string ryzen_supported_cpu_regex;
            public int ryzen_supported_minimum_series;
            public int intel_supported_minimum_series;
            public int intel_supported_minimum_generation;
            public int intel_ultra_supported_minimum_generation;
            public string intel_cpu_supported_version_regex;
            public string intel_ultra_cpu_supported_version_regex;
            public string rtx_gpu_supported_version_regex;
            public string rx_gpu_supported_version_regex;
            public string arc_gpu_supported_version_regex;
            public int minimum_rtx_supported_version;
            public int minimum_rx_supported_version;
            public int minimum_arc_supported_version;
            public string apple_silicon_supported_regex;
        }
    }
}
