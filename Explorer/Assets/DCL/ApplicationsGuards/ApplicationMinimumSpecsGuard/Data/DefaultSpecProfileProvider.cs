using System;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class DefaultSpecProfileProvider : ISpecProfileProvider
    {
        private const int MIN_VRAM_MB = 6 * 1024;
        private const int MIN_RAM_MB = 16 * 1024;
        private const int MIN_STORAGE_MB = 8 * 1024;
        
        private const string WIN_OS_REQ = "Windows 10 or newer";
        private const string WIN_CPU_REQ = "Intel i5 (7th Gen) or AMD Ryzen 5+";
        private const string WIN_GPU_REQ = "Nvidia RTX 20 series or AMD RX 5000 series+ (DirectX 12 compatible)";
        private const string WIN_SHADER_REQ = "Compute Shaders";
        private const string WIN_GPU_INTEGRATED_REQ = "A dedicated GPU is required. The app may not run correctly on integrated graphics.";
        
        private const string MAC_OS_REQ = "macOS 11 (Big Sur) or newer";
        private const string MAC_CPU_REQ = "Apple M1 or newer";
        private const string MAC_GPU_REQ = "Apple M1 Integrated";
        private const string MAC_SHADER_REQ = "Metal-compatible (Compute Shaders)";

        public SpecProfile GetProfile(PlatformOS platform)
        {
            return platform switch
            {
                PlatformOS.Windows => new SpecProfile
                {
                    // Checks
                    OsCheck = SystemSpecUtils.IsWindowsVersionAcceptable, CpuCheck = SystemSpecUtils.IsWindowsCpuAcceptable, GpuCheck = SystemSpecUtils.IsWindowsGpuAcceptable, IsIntegratedGpuCheck = SystemSpecUtils.IsIntegratedGpu,
                    ShaderCheck = SystemSpecUtils.ComputeShaderCheck,
                    DirectX12Check = SystemSpecUtils.IsDirectX12Compatible,

                    // Display Strings
                    OsRequirement = WIN_OS_REQ, CpuRequirement = WIN_CPU_REQ, GpuRequirement = WIN_GPU_REQ, ShaderRequirement = WIN_SHADER_REQ,
                    GpuIntegratedRequirement = WIN_GPU_INTEGRATED_REQ,

                    // Numeric Values
                    MinVramMB = MIN_VRAM_MB, MinRamMB = MIN_RAM_MB, MinStorageMB = MIN_STORAGE_MB
                },

                PlatformOS.Mac => new SpecProfile
                {
                    // Checks
                    OsCheck = SystemSpecUtils.IsMacOSVersionAcceptable, CpuCheck = SystemSpecUtils.IsAppleSilicon, GpuCheck = SystemSpecUtils.IsAppleSilicon, IsIntegratedGpuCheck = _ => false,
                    ShaderCheck = SystemSpecUtils.ComputeShaderCheck,

                    // Display Strings
                    OsRequirement = MAC_OS_REQ, CpuRequirement = MAC_CPU_REQ, GpuRequirement = MAC_GPU_REQ, GpuIntegratedRequirement = string.Empty,
                    ShaderRequirement = MAC_SHADER_REQ,

                    // Numeric Values
                    MinVramMB = MIN_VRAM_MB, MinRamMB = MIN_RAM_MB, MinStorageMB = MIN_STORAGE_MB
                },

                _ => throw new NotSupportedException($"Platform '{platform}' is not supported.")
            };
        }
    }
}