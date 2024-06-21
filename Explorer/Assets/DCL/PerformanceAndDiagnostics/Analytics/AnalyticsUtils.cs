using Segment.Serialization;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public static class AnalyticsExtensions
    {
        public static void SendSystemInfo(this IAnalyticsService analytics)
        {
            analytics.Track("system_info_report", new JsonObject
            {
                ["device_type"] = SystemInfo.deviceType.ToString(), // Desktop, Console, Handeheld (Mobile), Unknown
                ["device_model"] = SystemInfo.deviceModel, // "XPS 17 9720 (Dell Inc.)"
                ["operating_system"] = SystemInfo.operatingSystem, // "Windows 11  (10.0.22631) 64bit"

                ["processor_type"] = SystemInfo.processorType, // 🟢 "12th Gen Intel(R) Core(TM) i7-12700H"
                ["processor_count"] = SystemInfo.processorCount, // 🟢 20

                ["graphics_device_name"] = SystemInfo.graphicsDeviceName, // 🟢 "NVIDIA GeForce RTX 3050 Laptop GPU"
                ["graphics_memory_size"] = SystemInfo.graphicsMemorySize, // 🟢 3965 in [MB]

                ["system_memory_size"] = SystemInfo.systemMemorySize, // 🟢 65220 in [MB]

                ["graphics_device_type"] = SystemInfo.graphicsDeviceType.ToString(), // "Direct3D11", Vulkan, OpenGLCore, XBoxOne...
                ["graphics_device_version"] = SystemInfo.graphicsDeviceVersion, // "Direct3D 11.0 [level 11.1]"
            });
        }
    }
}
