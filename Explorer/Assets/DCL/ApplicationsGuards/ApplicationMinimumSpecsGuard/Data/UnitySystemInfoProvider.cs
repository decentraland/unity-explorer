using UnityEngine.Device;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public interface ISystemInfoProvider
    {
        string OperatingSystem { get; }
        string ProcessorType { get; }
        string GraphicsDeviceName { get; }
        int GraphicsMemorySize { get; }
        int SystemMemorySize { get; }
    }

    public class UnitySystemInfoProvider : ISystemInfoProvider
    {
        public string OperatingSystem => SystemInfo.operatingSystem;
        public string ProcessorType => SystemInfo.processorType;
        public string GraphicsDeviceName => SystemInfo.graphicsDeviceName;
        public int GraphicsMemorySize => SystemInfo.graphicsMemorySize;
        public int SystemMemorySize => SystemInfo.systemMemorySize;
    }
}