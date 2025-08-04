namespace DCL.ApplicationMinimumSpecsGuard.Tests
{
    /// <summary>
    ///     A fake implementation of ISystemInfoProvider for testing purposes.
    ///     It allows setting mock values for system specs.
    /// </summary>
    public class FakeSystemInfoProvider : ISystemInfoProvider
    {
        public string OperatingSystem { get; set; } = "Windows 10";
        public string ProcessorType { get; set; } = "Intel Core i9-9900K";
        public string GraphicsDeviceName { get; set; } = "NVIDIA GeForce RTX 2080";
        public int GraphicsMemorySize { get; set; } = 8192;
        public int SystemMemorySize { get; set; } = 16384;
    }
}