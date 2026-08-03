using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UUAV.Editor
{
    /// <summary>Copies uuav-adapter into the built player's plugin dir, beside the client library that spawns it.</summary>
    /// <remarks>The adapter's DefaultImporter .meta stops Unity treating it as a plugin - but also stops Unity auto-copying it into a build.</remarks>
    public sealed class AdapterBuildPostprocessor : IPostprocessBuildWithReport
    {
        private const string MACOS_ADAPTER_GUID = "c58e85d0786640ba88e682f96778cbd7";
        private const string WINDOWS_ADAPTER_GUID = "1bf90320e606400eacb05b9dfda31771";

        private const string MACOS_CLIENT_LIBRARY = "libuuav.dylib";
        private const string WINDOWS_CLIENT_LIBRARY = "uuav.dll";

        private const int EXECUTABLE_FILE_MODE = 493;

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            string adapterGuid;
            string clientLibrary;
            bool isMacOS;

            switch (report.summary.platform)
            {
                case BuildTarget.StandaloneOSX:
                    adapterGuid = MACOS_ADAPTER_GUID;
                    clientLibrary = MACOS_CLIENT_LIBRARY;
                    isMacOS = true;
                    break;
                case BuildTarget.StandaloneWindows64:
                    adapterGuid = WINDOWS_ADAPTER_GUID;
                    clientLibrary = WINDOWS_CLIENT_LIBRARY;
                    isMacOS = false;
                    break;
                default:
                    return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(adapterGuid);

            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                Debug.Log(
                    "[UUAV] no adapter binary is deployed in the project, so none was added to the "
                    + "player. Deploy one with `UUAV_BUILD_ADAPTER=1 bash build.sh` in the native folder.");
                return;
            }

            string outputPath = report.summary.outputPath;

            string pluginRoot = isMacOS
                ? Path.Combine(outputPath, "Contents", "PlugIns")
                : WindowsPluginRoot(outputPath);

            string fileName = Path.GetFileName(assetPath);
            string destination = Path.Combine(PluginDirectory(pluginRoot, clientLibrary), fileName);

            File.Copy(Path.GetFullPath(assetPath), destination, true);

            if (isMacOS)
                MarkExecutable(destination);

            Debug.Log($"[UUAV] copied {fileName} into {destination}");
        }

        /// <summary>Windows Plugins dir, from the exe path since build options can override the product name.</summary>
        private static string WindowsPluginRoot(string executablePath)
        {
            string? buildDirectory = Path.GetDirectoryName(executablePath);

            if (string.IsNullOrEmpty(buildDirectory))
                throw new BuildFailedException(
                    $"UUAV: cannot derive the player directory from the build output path '{executablePath}'.");

            return Path.Combine(buildDirectory, $"{Path.GetFileNameWithoutExtension(executablePath)}_Data", "Plugins");
        }

        /// <summary>Directory <paramref name="clientLibrary"/> landed in - the only place the client looks for the adapter.</summary>
        private static string PluginDirectory(string pluginRoot, string clientLibrary)
        {
            if (Directory.Exists(pluginRoot))
            {
                if (File.Exists(Path.Combine(pluginRoot, clientLibrary)))
                    return pluginRoot;

                foreach (string subdirectory in Directory.EnumerateDirectories(pluginRoot, "*", SearchOption.AllDirectories))
                {
                    if (File.Exists(Path.Combine(subdirectory, clientLibrary)))
                        return subdirectory;
                }
            }

            throw new BuildFailedException(
                $"UUAV: the built player has no {clientLibrary} under '{pluginRoot}', so there is "
                + "nowhere to put the adapter beside it.");
        }

        /// <summary>File.Copy keeps an existing destination's mode, so re-chmod every build or a stale non-exec leftover ships broken.</summary>
        private static void MarkExecutable(string path)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
                return;

            if (Chmod(path, EXECUTABLE_FILE_MODE) != 0)
                throw new BuildFailedException($"UUAV: chmod 0755 failed for '{path}'; the player cannot exec the adapter.");
        }

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string pathname, int mode);
    }
}
