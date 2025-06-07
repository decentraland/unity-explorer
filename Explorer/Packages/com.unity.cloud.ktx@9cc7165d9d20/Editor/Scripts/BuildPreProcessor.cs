// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

#if UNITY_2023_3_OR_NEWER || UNITY_2022_3
#define VISION_OS_SUPPORTED
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace KtxUnity.Editor
{
    class BuildPreProcessor : IPreprocessBuildWithReport
    {
        public const string packagePath = "Packages/com.unity.cloud.ktx/Runtime/Plugins/";

        internal static readonly Dictionary<GUID, int> webAssemblyLibraries = new Dictionary<GUID, int>()
        {
            // Database of WebAssembly library files within folder `Runtime/Plugins/WebGL`
            [new GUID("143eb4e0158994750a74465b1c68a52b")] = 2020, // 2020/libktx_read.bc
            [new GUID("d5d7736ff60a64272adca7a7c3635175")] = 2020, // 2020/libktx_unity.bc
            [new GUID("c2217daa2f255429fac9f7ad37ededb5")] = 2020, // 2020/libobj_basisu_cbind.bc
            [new GUID("df97b0e93a9ce4dfea9b19bb84c197aa")] = 2021, // 2021/libktx_read.a
            [new GUID("ad44f70cce67349758a1f872354c25be")] = 2021, // 2021/libktx_unity.a
            [new GUID("c3d638c4775624a4aa8a0124da084d8c")] = 2021, // 2021/libobj_basisu_cbind.a
            [new GUID("1903498fc70cf40f698bc7cb3f3b616f")] = 2022, // 2022/libktx_read.a
            [new GUID("39f63d50e71334f7886493189c281dd9")] = 2022, // 2022/libktx_unity.a
            [new GUID("56a5eafddecc942128d8c15652750b74")] = 2022, // 2022/libobj_basisu_cbind.a
            [new GUID("064f9fdd6ee9346269b838d6b768b3cc")] = 2023, // 2023/libktx_read.a
            [new GUID("b8faaa868093c46ddab6e9538d1625e6")] = 2023, // 2023/libktx_unity.a
            [new GUID("22f5fcc807c2544dda814ef9d61f68ad")] = 2023, // 2023/libobj_basisu_cbind.a
        };

        public int callbackOrder => 0;

        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            SetRuntimePluginCopyDelegate(report.summary.platform);
        }

        static void SetRuntimePluginCopyDelegate(BuildTarget platform)
        {
            var allPlugins = PluginImporter.GetImporters(platform);
            foreach (var plugin in allPlugins)
            {
                if (plugin.isNativePlugin
                    && plugin.assetPath.StartsWith(packagePath)
                   )
                {
                    switch (platform)
                    {
                        case BuildTarget.iOS:
                        case BuildTarget.tvOS:
#if VISION_OS_SUPPORTED
                        case BuildTarget.VisionOS:
#endif
                            plugin.SetIncludeInBuildDelegate(IncludeAppleLibraryInBuild);
                            break;
                        case BuildTarget.WebGL:
                            if (webAssemblyLibraries.Keys.Any(libGuid => libGuid == AssetDatabase.GUIDFromAssetPath(plugin.assetPath)))
                            {
                                plugin.SetIncludeInBuildDelegate(IncludeWebLibraryInBuild);
                            }
                            break;
                    }
                }
            }
        }

        static bool IsSimulatorBuild(BuildTarget platformGroup)
        {
            switch (platformGroup)
            {
                case BuildTarget.iOS:
                    return PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK;
                case BuildTarget.tvOS:
                    return PlayerSettings.tvOS.sdkVersion == tvOSSdkVersion.Simulator;
#if VISION_OS_SUPPORTED
                case BuildTarget.VisionOS:
                    return PlayerSettings.VisionOS.sdkVersion == VisionOSSdkVersion.Simulator;
#endif
            }

            return false;
        }

        static bool IncludeAppleLibraryInBuild(string path)
        {
            var isSimulatorLibrary = IsAppleSimulatorLibrary(path);
            var isSimulatorBuild = IsSimulatorBuild(EditorUserBuildSettings.activeBuildTarget);
            return isSimulatorLibrary == isSimulatorBuild;
        }

        static bool IncludeWebLibraryInBuild(string path)
        {
            return IsWebAssemblyCompatible(path);
        }

        public static bool IsAppleSimulatorLibrary(string assetPath)
        {
            var parent = new DirectoryInfo(assetPath).Parent;

            switch (parent?.Name)
            {
                case "Simulator":
                    return true;
                case "Device":
                    return false;
                default:
                    throw new InvalidDataException(
                        $@"Could not determine SDK type of library ""{assetPath}"". " +
                        @"Apple iOS/tvOS/visionOS native libraries have to be placed in a folder named ""Device"" " +
                        @"or ""Simulator"" for implicit SDK type detection."
                    );
            }
        }

        static bool IsWebAssemblyCompatible(string assetPath)
        {
            var unityVersion = new UnityVersion(Application.unityVersion);

            var pluginGuid = AssetDatabase.GUIDFromAssetPath(assetPath);

            return IsWebAssemblyCompatible(pluginGuid, unityVersion);
        }

        public static bool IsWebAssemblyCompatible(GUID pluginGuid, UnityVersion unityVersion)
        {
            var wasm2021 = new UnityVersion("2021.2");
            var wasm2022 = new UnityVersion("2022.2");
            var wasm2023 = new UnityVersion("2023.2.0a17");

            if (webAssemblyLibraries.TryGetValue(pluginGuid, out var majorVersion))
            {
                switch (majorVersion)
                {
                    case 2020:
                        return unityVersion < wasm2021;
                    case 2021:
                        return unityVersion >= wasm2021 && unityVersion < wasm2022;
                    case 2022:
                        return unityVersion >= wasm2022 && unityVersion < wasm2023;
                    case 2023:
                        return unityVersion >= wasm2023;
                }
            }

            throw new InvalidDataException($"Unknown WebAssembly library at {AssetDatabase.GUIDToAssetPath(pluginGuid)}.");
        }
    }
}
