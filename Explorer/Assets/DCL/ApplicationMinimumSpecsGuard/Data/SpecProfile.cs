using System;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public enum SpecCategory
    {
        OS,
        CPU,
        GPU,
        VRAM,
        RAM,
        Storage,
        ComputeShaders
    }

    public enum PlatformOS
    {
        Windows,
        Mac,
        Unsupported
    }

    public readonly struct SpecResult
    {
        public readonly SpecCategory Category;
        public readonly bool IsMet;
        public readonly string Required;
        public readonly string Actual;

        public SpecResult(SpecCategory category, bool isMet, string required, string actual)
        {
            Category = category;
            IsMet = isMet;
            Required = required;
            Actual = actual;
        }
    }

    public class SpecProfile
    {
        public string PlatformLabel;

        public string OsRequirement;
        public Func<string, bool> OsCheck;

        public string CpuRequirement;
        public Func<string, bool> CpuCheck;

        public string GpuRequirement;
        public Func<string, bool> GpuCheck;

        public int MinRamMB;
        public string RamRequirement => $"{MinRamMB / 1024f} GB";

        public int MinVramMB;
        public string VramRequirement => $"{MinVramMB / 1024f} GB";

        public string ShaderRequirement;
        public Func<bool> ShaderCheck;
        public Func<bool> DirectX12Check;

        public int MinStorageMB;
        public string StorageRequirement => $"{MinStorageMB / 1024f} GB";
    }

    public interface ISpecProfileProvider
    {
        SpecProfile GetProfile(PlatformOS platform);
    }
}