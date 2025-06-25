using System;
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
                    CpuCheck = SystemSpecUtils.IsWindowsCpuAcceptable, GpuRequirement = "Nvidia RTX 20 Series or AMD Radeon RX 5000 Series (DirectX 12 Compatible)", GpuCheck = SystemSpecUtils.IsWindowsGpuAcceptable, MinVramGB = 6,
                    MinRamGB = 16, ShaderRequirement = "Compute Shaders (DX12)", ShaderCheck = () => SystemInfo.supportsComputeShaders, MinStorageGB = 8
                },

                PlatformOS.Mac when target == SpecTarget.Minimum => new SpecProfile
                {
                    PlatformLabel = "macOS", OsRequirement = "macOS 11 Big Sur", OsCheck = os => os.Contains("Mac OS X") && SystemSpecUtils.TryGetMacVersionMajor(os) >= 11, CpuRequirement = "Apple M1",
                    CpuCheck = SystemSpecUtils.IsAppleSilicon, GpuRequirement = "Apple M1 (Metal support)", GpuCheck = SystemSpecUtils.IsAppleSilicon, MinVramGB = 6,
                    MinRamGB = 16, ShaderRequirement = "Metal-compatible (Compute Shaders)", ShaderCheck = () => SystemInfo.supportsComputeShaders, MinStorageGB = 8
                },

                _ => throw new NotSupportedException("Unsupported platform")
            };
        }
    }
}