using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class DefaultSpecProfileProvider : ISpecProfileProvider
    {
        public SpecProfile GetProfile(PlatformOS platform, SpecTarget target)
        {
            return platform switch
            {
                PlatformOS.Windows when target == SpecTarget.Minimum => new SpecProfile
                {
                    PlatformLabel = "Windows", OsRequirement = "Windows 10 64-bit", OsCheck = os => os.Contains("Windows 10") || os.Contains("Windows 11"), CpuRequirement = "Intel i5 7th gen or AMD Ryzen 5+",
                    CpuCheck = IsWindowsCpuAcceptable, GpuRequirement = "Nvidia RTX 20 Series or AMD Radeon RX 5000 Series (DirectX 12 Compatible)", GpuCheck = IsWindowsGpuAcceptable, MinVramGB = 6,
                    MinRamGB = 15, ShaderRequirement = "Compute Shaders (DX12)", ShaderCheck = () => SystemInfo.supportsComputeShaders, MinStorageGB = 8
                },

                PlatformOS.Mac when target == SpecTarget.Minimum => new SpecProfile
                {
                    PlatformLabel = "macOS", OsRequirement = "macOS 11 Big Sur", OsCheck = os => os.Contains("Mac OS X") && TryGetMacVersionMajor(os) >= 11, CpuRequirement = "Apple M1",
                    CpuCheck = IsAppleSilicon, GpuRequirement = "Apple M1 (Metal support)", GpuCheck = IsAppleSilicon, MinVramGB = 6,
                    MinRamGB = 16, ShaderRequirement = "Metal-compatible (Compute Shaders)", ShaderCheck = () => SystemInfo.supportsComputeShaders, MinStorageGB = 8
                },

                _ => throw new NotSupportedException("Unsupported platform")
            };
        }

        private static bool IsWindowsCpuAcceptable(string cpu)
        {
            cpu = cpu.ToLowerInvariant();

            // AMD Ryzen 5/7/9
            if (cpu.Contains("ryzen"))
            {
                var match = Regex.Match(cpu, @"ryzen\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int model))
                    return model >= 5; // Ryzen 5, 7, 9
            }

            // Matches Intel CPUs like "i5-7400" or "i7-11700K" and captures the model number (e.g., 7400, 11700)
            // Intel i5+ with 7th gen+
            var intelMatch = Regex.Match(cpu, @"i[3579]-?(\d{4})");
            if (intelMatch.Success && int.TryParse(intelMatch.Groups[1].Value, out int modelNumber))
                return modelNumber >= 7000;

            return false;
        }

        private static bool IsWindowsGpuAcceptable(string gpu)
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

            return false;
        }

        /// <summary>
        ///     Checks if the provided device name corresponds to an Apple M-series processor (M1, M2, etc.).
        ///     This is used for both CPU and GPU on Apple Silicon systems.
        /// </summary>
        private static bool IsAppleSilicon(string deviceName)
        {
            // The regex is now hidden as an implementation detail, which is perfect.
            return Regex.IsMatch(deviceName, @"apple\s+m\d", RegexOptions.IgnoreCase);
        }

        private static int TryGetMacVersionMajor(string os)
        {
            var match = Regex.Match(os, @"Mac OS X (\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }
}