using System.Collections.Generic;
using DCL.Diagnostics;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsGuard
    {
        private readonly ISpecProfileProvider profileProvider;
        private List<SpecResult> cachedResults = new();

        public IReadOnlyList<SpecResult> Results => cachedResults;

        public MinimumSpecsGuard(ISpecProfileProvider profileProvider)
        {
            this.profileProvider = profileProvider;
        }

        /// <summary>
        ///     MinimumSpecsGuard performs hardware requirement validation for Decentraland.
        ///     It checks if the current system meets platform-specific minimum specs (RAM, VRAM, GPU, CPU, OS, Shader).
        ///     Results are cached and exposed for use in UI or analytics.
        ///     Reference: https://docs.decentraland.org/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland
        ///     ------------------------------
        ///     Minimum Requirements
        ///     ------------------------------
        ///     Windows:
        ///     - OS:      Windows 10 64-bit
        ///     - CPU:     Intel i5 7th gen or AMD Ryzen 5
        ///     - GPU:     NVIDIA RTX 20 Series or AMD RX 5000+ (DirectX 12 Compatible if compute shaders are available)
        ///     - VRAM:    6 GB
        ///     - RAM:     16 GB
        ///     - Storage:  64 GB HDD
        ///     macOS:
        ///     - OS:      macOS 11 Big Sur
        ///     - CPU:     Apple M1
        ///     - GPU:     Apple M1 integrated (Metal support)
        ///     - VRAM:    6 GB (shared)
        ///     - RAM:     16 GB
        ///     - Storage:  64 GB HDD
        ///     ------------------------------
        ///     Usage:
        ///     ------------------------------
        ///     var guard = new MinimumSpecsGuard(new DefaultSpecProfileProvider());
        ///     bool isCompatible = guard.HasMinimumSpecs();
        ///     results = guard.Results;
        /// </summary>
        private List<SpecResult> Evaluate(SpecTarget target)
        {
            var platform = PlatformUtils.DetectPlatform();
            var profile = profileProvider.GetProfile(platform, target);

            var results = new List<SpecResult>();

            // OS
            string os = SystemInfo.operatingSystem;
            results.Add(new SpecResult(SpecCategory.OS, profile.OsCheck(os), profile.OsRequirement, os));

            // CPU
            string cpu = SystemInfo.processorType;
            results.Add(new SpecResult(SpecCategory.CPU, profile.CpuCheck(cpu), profile.CpuRequirement, cpu));

            // GPU
            string dx12Tag = profile.ShaderCheck() ? "(DirectX 12 Compatible)" : "(Not DirectX 12 Compatible)";
            string gpu = $"{SystemInfo.graphicsDeviceName} {dx12Tag}";
            results.Add(new SpecResult(SpecCategory.GPU, profile.GpuCheck(gpu), profile.GpuRequirement, gpu));

            // VRAM
            int vram = SystemInfo.graphicsMemorySize;
            results.Add(new SpecResult(SpecCategory.VRAM, vram >= profile.MinVramMB, profile.VramRequirement, $"{vram / 1024f:0.0} GB"));

            // RAM
            int ram = SystemInfo.systemMemorySize;
            results.Add(new SpecResult(SpecCategory.RAM, ram >= profile.MinRamMB, profile.RamRequirement, $"{ram / 1024f:0.0} GB"));

            // Storage
            long availableStorageBytes = PlatformUtils.GetAvailableStorageBytes(Application.persistentDataPath);
            float availableStorageGB = availableStorageBytes / (1024f * 1024f * 1024f);

            results.Add(new SpecResult(
                SpecCategory.Storage,
                availableStorageGB >= profile.MinStorageGB,
                profile.StorageRequirement,
                $"{availableStorageGB:0.0} GB"));

            return results;
        }

        public bool HasMinimumSpecs(SpecTarget target = SpecTarget.Minimum)
        {
            cachedResults = Evaluate(target);
            bool allMet = true;

            foreach (var result in cachedResults)
            {
                string logMessage = $"Minimum spec {(result.IsMet ? "Met" : "Not met")}: " +
                                    $"{result.Category} - Required: {result.Required}, Actual: {result.Actual}";

                if (result.IsMet)
                    ReportHub.Log(ReportCategory.UNSPECIFIED, logMessage);
                else
                {
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, logMessage);
                    allMet = false;
                }
            }

            return allMet;
        }
    }
}
