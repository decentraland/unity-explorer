using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Linq;
using System.Text;
using UnityEngine.Device;

namespace DCL.Diagnostics
{
    public static class DiagnosticInfoUtils
    {
        public static void LogSystem(string version)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Clear();
            AppendHeader(stringBuilder, "DEVICE INFORMATION");

            // Device & OS
            stringBuilder.AppendFormat("Device Model: {0}\n", SystemInfo.deviceModel);
            stringBuilder.AppendFormat("Device Name: {0}\n", SystemInfo.deviceName);
            stringBuilder.AppendFormat("Operating System: {0}\n", SystemInfo.operatingSystem);
            stringBuilder.AppendFormat("System Language: {0}\n", Application.systemLanguage);
            stringBuilder.AppendFormat("Device Type: {0}\n", SystemInfo.deviceType);
            stringBuilder.AppendFormat("Device Unique ID: {0}\n\n", SystemInfo.deviceUniqueIdentifier);

            // CPU & Memory
            AppendHeader(stringBuilder, "HARDWARE");
            stringBuilder.AppendFormat("Processor Type: {0}\n", SystemInfo.processorType);
            stringBuilder.AppendFormat("Processor Count: {0}\n", SystemInfo.processorCount);
            stringBuilder.AppendFormat("Processor Frequency: {0} MHz\n", SystemInfo.processorFrequency);
            stringBuilder.AppendFormat("System Memory Size: {0} MB\n\n", SystemInfo.systemMemorySize);

            // Graphics
            AppendHeader(stringBuilder, "GRAPHICS");
            stringBuilder.AppendFormat("Graphics Device Name: {0}\n", SystemInfo.graphicsDeviceName);
            stringBuilder.AppendFormat("Graphics Device Type: {0}\n", SystemInfo.graphicsDeviceType);
            stringBuilder.AppendFormat("Graphics Memory Size: {0} MB\n", SystemInfo.graphicsMemorySize);
            stringBuilder.AppendFormat("Graphics Device Version: {0}\n", SystemInfo.graphicsDeviceVersion);
            stringBuilder.AppendFormat("Max Texture Size: {0}\n", SystemInfo.maxTextureSize);
            stringBuilder.AppendFormat("Supports Ray Tracing: {0}\n\n", SystemInfo.supportsRayTracing);

            // Unity & Application
            AppendHeader(stringBuilder, "APPLICATION");
            stringBuilder.AppendFormat("Unity Version: {0}\n", Application.unityVersion);
            stringBuilder.AppendFormat("Company Name: {0}\n", Application.companyName);
            stringBuilder.AppendFormat("Product Name: {0}\n", Application.productName);
            stringBuilder.AppendFormat("Version: {0}\n", version);
            stringBuilder.AppendFormat("Target Frame Rate: {0}\n", Application.targetFrameRate);
            stringBuilder.AppendFormat("Screen Resolution: {0}x{1}@{2}Hz\n",
            Screen.currentResolution.width,
            Screen.currentResolution.height,
            Screen.currentResolution.refreshRateRatio);


            ReportHub.LogProductionInfo(stringBuilder.ToString());
        }

        public static void LogEnvironment(IDecentralandUrlsSource decentralandUrlsSource)
        {
            var stringBuilder = new StringBuilder();
            AppendHeader(stringBuilder, "ENVIRONMENT");
            var environmentUrls = Enum.GetValues(typeof(DecentralandUrl)).Cast<DecentralandUrl>();
            foreach (var decentralandUrl in environmentUrls)
            {
                stringBuilder.AppendFormat("{0}: {1}\n", decentralandUrl.ToString(), decentralandUrlsSource.Url(decentralandUrl));
            }

            ReportHub.LogProductionInfo(stringBuilder.ToString());
        }

        private static void AppendHeader(StringBuilder stringBuilder, string header)
        {
            stringBuilder.AppendLine("==================");
            stringBuilder.AppendLine(header);
            stringBuilder.AppendLine("==================\n");
        }
    }
}
