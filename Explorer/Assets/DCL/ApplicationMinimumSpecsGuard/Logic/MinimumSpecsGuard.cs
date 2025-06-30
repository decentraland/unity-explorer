using System.Collections.Generic;
using DCL.Diagnostics;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace DCL.ApplicationMinimumSpecsGuard
{
    /// <summary>
    ///     Performs hardware validation against platform-specific minimum requirements.
    ///     This class checks the user's OS, CPU, GPU (and shader support), VRAM, RAM, available storage.
    ///     The results are cached and can be retrieved for display or analytics.
    ///     It is designed to be used early in the application's startup flow.
    ///     For official requirement details, see:
    ///     https://docs.decentraland.org/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland
    ///     // 1. Create the provider for spec profiles.
    ///     ISpecProfileProvider profileProvider = new DefaultSpecProfileProvider();
    ///     // 2. Instantiate the guard with the provider.
    ///     var specsGuard = new MinimumSpecsGuard(profileProvider);
    ///     // 3. Run the check. This returns true if all specs are met.
    ///     bool hasMinimumSpecs = specsGuard.HasMinimumSpecs();
    ///     // 4. (Optional) Get the detailed results for display.
    ///     IReadOnlyList
    ///     <SpecResult>
    ///         results = specsGuard.Results;
    ///         if (!hasMinimumSpecs)
    ///         {
    ///          
    ///         }
    /// </summary>
    public class MinimumSpecsGuard
    {
        public IReadOnlyList<SpecResult> Results => cachedResults;

        private readonly ISpecProfileProvider profileProvider;
        private List<SpecResult> cachedResults = new();

        public MinimumSpecsGuard(ISpecProfileProvider profileProvider)
        {
            this.profileProvider = profileProvider;
        }

        public bool HasMinimumSpecs(SpecTarget target = SpecTarget.Minimum)
        {
            cachedResults = Evaluate(target);
            bool allMet = true;

            foreach (var result in cachedResults)
            {
                if (!result.IsMet)
                {
                    allMet = false;
                    string logMessage = $"Minimum spec Not met: {result.Category} - Required: {result.Required}, Actual: {result.Actual}";
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, logMessage);
                }
            }

            ReportHub.Log(ReportCategory.UNSPECIFIED, $"Minimum specs check complete. All requirements met: {allMet}");
            return allMet;
        }

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
            string gpuName = SystemInfo.graphicsDeviceName;
            bool isGpuModelAcceptable = profile.GpuCheck(gpuName);
            bool hasRequiredFeatures = profile.ShaderCheck();
            bool isGpuSpecMet = isGpuModelAcceptable && hasRequiredFeatures;

            string featureTag;
            switch (platform)
            {
                case PlatformOS.Windows:
                    featureTag = isGpuSpecMet ? "(DirectX 12 Compatible)" : "(Not DirectX 12 Compatible)";
                    break;

                case PlatformOS.Mac:
                    featureTag = "(Metal Compatible)";
                    break;

                default:
                    featureTag = hasRequiredFeatures ? "(Compute Shaders Supported)" : "";
                    break;
            }

            string actualGpuDisplayString = $"{gpuName} {featureTag}".Trim();

            results.Add(new SpecResult(
                SpecCategory.GPU,
                isGpuSpecMet,
                profile.GpuRequirement,
                actualGpuDisplayString
            ));

            // VRAM
            int actualVramMB = SystemInfo.graphicsMemorySize;
            int vramGB = Mathf.CeilToInt(actualVramMB / 1024f);
            results.Add(new SpecResult(SpecCategory.VRAM, actualVramMB >= profile.MinVramMB, profile.VramRequirement, $"{vramGB} GB"));

            // RAM
            int actualRamMB = SystemInfo.systemMemorySize;
            int ramGB = Mathf.CeilToInt(actualRamMB / 1024f);
            results.Add(new SpecResult(SpecCategory.RAM, actualRamMB >= profile.MinRamMB, profile.RamRequirement, $"{ramGB} GB"));

            // Storage
            long availableStorageBytes = PlatformUtils.GetAvailableStorageBytes(Application.persistentDataPath);
            float actualAvailableStorageMB = availableStorageBytes / (1024f * 1024f);
            float availableStorageGB = Mathf.CeilToInt(availableStorageBytes / (1024f * 1024f * 1024f));

            results.Add(new SpecResult(
                SpecCategory.Storage,
                actualAvailableStorageMB >= profile.MinStorageMB,
                profile.StorageRequirement,
                $"{availableStorageGB} GB"));

            return results;
        }
    }
}