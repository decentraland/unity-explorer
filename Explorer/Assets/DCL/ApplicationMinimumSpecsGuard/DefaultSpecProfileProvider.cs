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
                    CpuCheck = IsWindowsCpuAcceptable, GpuRequirement = "Nvidia RTX 20 Series or AMD Radeon RX 5000 Series (DirectX 12 Compatible)", GpuCheck = IsWindowsGpuAcceptable, MinVramMB = 6144,
                    VramRequirement = "6 GB", MinRamMB = 16384, RamRequirement = "16 GB", ShaderRequirement = "Compute Shaders (DX12)",
                    ShaderCheck = () => SystemInfo.supportsComputeShaders, MinStorageGB = 8, StorageRequirement = "8 GB"
                },

                PlatformOS.Mac when target == SpecTarget.Minimum => new SpecProfile
                {
                    PlatformLabel = "macOS", OsRequirement = "macOS 11 Big Sur", OsCheck = os => os.Contains("Mac OS X") && TryGetMacVersionMajor(os) >= 11, CpuRequirement = "Apple M1",
                    CpuCheck = cpu => cpu.ToLower().Contains("apple m1") || cpu.ToLower().Contains("apple m2"), GpuRequirement = "Apple M1 (Metal support)", GpuCheck = gpu => gpu.ToLower().Contains("apple m1") || gpu.ToLower().Contains("apple m2"), MinVramMB = 6144,
                    VramRequirement = "6 GB", MinRamMB = 16384, RamRequirement = "16 GB", ShaderRequirement = "Metal-compatible (Compute Shaders)",
                    ShaderCheck = () => SystemInfo.supportsComputeShaders, MinStorageGB = 8, StorageRequirement = "8 GB"
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

        private static int TryGetMacVersionMajor(string os)
        {
            var match = Regex.Match(os, @"Mac OS X (\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }
}