using System;
using System.Collections.Generic;
using System.IO;
using DCL.Diagnostics;
using UnityEngine;
using Application = UnityEngine.Device.Application;

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
        private readonly ISystemInfoProvider systemInfoProvider;
        private readonly IDriveInfoProvider driveInfoProvider;
        private List<SpecResult> cachedResults = new();

        public MinimumSpecsGuard(ISpecProfileProvider profileProvider,
            ISystemInfoProvider systemInfoProvider,
            IDriveInfoProvider driveInfoProvider)
        {
            this.profileProvider = profileProvider;
            this.systemInfoProvider = systemInfoProvider;
            this.driveInfoProvider = driveInfoProvider;
        }

        public bool HasMinimumSpecs()
        {
            cachedResults = Evaluate();
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

        private List<SpecResult> Evaluate()
        {
            var platform = PlatformUtils.DetectPlatform();
            var profile = profileProvider.GetProfile(platform);

            var results = new List<SpecResult>();

            // OS
            string os = systemInfoProvider.OperatingSystem;
            results.Add(new SpecResult(SpecCategory.OS, profile.OsCheck(os), profile.OsRequirement, os));

            // CPU
            string cpu = systemInfoProvider.ProcessorType;
            results.Add(new SpecResult(SpecCategory.CPU, profile.CpuCheck(cpu), profile.CpuRequirement, cpu));

            // GPU
            string gpuName = systemInfoProvider.GraphicsDeviceName;
            bool isGpuModelAcceptable = profile.GpuCheck(gpuName);
            bool hasRequiredFeatures = profile.ShaderCheck();
            bool isGpuSpecMet = isGpuModelAcceptable && hasRequiredFeatures;

            // Integrated GPU check
            string gpuRequirementMessage = profile.GpuRequirement;
            if (!isGpuModelAcceptable && profile.IsIntegratedGpuCheck(gpuName))
            {
                gpuRequirementMessage = profile.GpuIntegratedRequirement;
            }
            
            string actualGpuDisplayString = $"{gpuName}".Trim();

            results.Add(new SpecResult(
                SpecCategory.GPU,
                isGpuSpecMet,
                gpuRequirementMessage,
                actualGpuDisplayString
            ));

            // VRAM
            int actualVramMB = systemInfoProvider.GraphicsMemorySize;
            bool isVramMet = SystemSpecUtils.IsMemorySizeSufficient(actualVramMB, profile.MinVramMB);
            int roundedActualVramGB = Mathf.RoundToInt(actualVramMB / 1024f);
            
            // NOTE: e.g., shows "16 GB" not "15.7 GB"
            string actualVramDisplay = $"{roundedActualVramGB} GB";
            results.Add(new SpecResult(SpecCategory.VRAM, isVramMet, profile.VramRequirement, actualVramDisplay));

            // RAM
            int actualRamMB = systemInfoProvider.SystemMemorySize;
            bool isRamMet = SystemSpecUtils.IsMemorySizeSufficient(actualRamMB, profile.MinRamMB);
            int roundedActualRamGB = Mathf.RoundToInt(actualRamMB / 1024f);
            
            // NOTE: e.g., shows "16 GB" not "15.7 GB"
            string actualRamDisplay = $"{roundedActualRamGB} GB";
            results.Add(new SpecResult(SpecCategory.RAM, isRamMet, profile.RamRequirement, actualRamDisplay));

            try
            {
                const float BYTES_TO_MB = 1f / (1024f * 1024f);
                const float BYTES_TO_GB = 1f / (1024f * 1024f * 1024f);

                // 1) Prefer the direct probe of the volume we actually use
                var primary = Utility.PlatformUtils.GetPrimaryStorageInfoUsingPersistentPath();

                if (primary != null)
                {
                    float availableMB = primary.AvailableFreeSpace * BYTES_TO_MB;
                    float availableGB = primary.AvailableFreeSpace * BYTES_TO_GB;

                    results.Add(new SpecResult(
                        SpecCategory.Storage,
                        availableMB >= profile.MinStorageMB,
                        profile.StorageRequirement,
                        $"{availableGB:F1} GB"
                    ));
                }
                else
                {
                    var allDrives = driveInfoProvider.GetDrivesInfo();

                    if (allDrives.Count > 0)
                    {
                        string persistentPath = driveInfoProvider.GetPersistentDataPath();
                        persistentPath = persistentPath.Replace('/', Path.DirectorySeparatorChar);
                        Utility.PlatformUtils.DriveData targetDrive = null;
                        int longestMatchLength = -1;

                        // This loop finds the best match. On macOS, paths can be nested (e.g., "/" and "/System/Volumes/Data").
                        // We need to find the longest mount point that is a prefix of our path. This works for both macOS and Windows.
                        foreach (var drive in allDrives)
                        {
                            if (persistentPath.StartsWith(drive.Name, StringComparison.OrdinalIgnoreCase) && drive.Name.Length > longestMatchLength)
                            {
                                targetDrive = drive;
                                longestMatchLength = drive.Name.Length;
                            }
                        }

                        if (targetDrive != null)
                        {
                            float actualAvailableStorageMB = targetDrive.AvailableFreeSpace / (1024f * 1024f);
                            float availableStorageGB = targetDrive.AvailableFreeSpace / (1024f * 1024f * 1024f);

                            results.Add(new SpecResult(
                                SpecCategory.Storage,
                                actualAvailableStorageMB >= profile.MinStorageMB,
                                profile.StorageRequirement,
                                $"{availableStorageGB:F1} GB"
                            ));
                        }
                        else
                        {
                            throw new Exception($"Could not find a mounted drive corresponding to the path: {persistentPath}");
                        }
                    }
                    else
                    {
                        results.Add(new SpecResult(SpecCategory.Storage, false, profile.StorageRequirement, "No drives found"));
                    }
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UNSPECIFIED);
                results.Add(new SpecResult(SpecCategory.Storage, false, profile.StorageRequirement, "Error determining space"));
            }

            return results;
        }
    }
}