using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UUAV.Editor
{
    /// <summary>
    /// Ships the out-of-process decode helper with standalone builds.
    ///
    /// Unity copies only recognized plugin files (.dll/.dylib) out of Plugins
    /// folders; the helper executable imports as a DefaultAsset and must be
    /// placed next to the deployed uuav plugin manually, where the client
    /// dylib resolves it (and where the FFmpeg libraries resolve for the
    /// helper itself).
    /// </summary>
    internal sealed class HelperBuildPostprocessor : IPostprocessBuildWithReport
    {
        // the UUAV "package" folder is imported as plain Assets content
        // (not UPM-mounted), so the source path is project-relative
        private const string PACKAGE_PLUGINS = "Assets/Plugins/UUAV/Packages/UUAV/Runtime/Plugins";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            switch (report.summary.platform)
            {
                case BuildTarget.StandaloneWindows64:
                    CopyWindowsHelper(report.summary.outputPath);
                    break;
                case BuildTarget.StandaloneOSX:
                    CopyMacHelper(report.summary.outputPath);
                    break;
            }
        }

        private static void CopyWindowsHelper(string exePath)
        {
            var dataDirectory = Path.Combine(
                Path.GetDirectoryName(exePath)!,
                Path.GetFileNameWithoutExtension(exePath) + "_Data"
            );
            var destination = Path.Combine(dataDirectory, "Plugins", "x86_64", "uuav-helper.exe");
            Copy(Path.GetFullPath($"{PACKAGE_PLUGINS}/x86_64/uuav-helper.exe"), destination);
        }

        private static void CopyMacHelper(string appPath)
        {
            var destination = Path.Combine(appPath, "Contents", "PlugIns", "uuav-helper");
            Copy(Path.GetFullPath($"{PACKAGE_PLUGINS}/macOS/uuav-helper"), destination);
            MakeExecutable(destination);
        }

        private static void Copy(string source, string destination)
        {
            if (File.Exists(source) == false)
            {
                throw new BuildFailedException(
                    $"[UUAV] helper executable missing at {source}; run native/build.sh"
                );
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            Debug.Log($"[UUAV] deployed helper to {destination}");
        }

        // File.Copy does not preserve the executable bit; restore it so the
        // client dylib can spawn the helper from inside the bundle. Only
        // meaningful when building on macOS itself.
        private static void MakeExecutable(string path)
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                Debug.LogWarning(
                    $"[UUAV] cannot set the executable bit on {path} from this OS; fix it before shipping the .app"
                );
                return;
            }

            using var chmod = System.Diagnostics.Process.Start("/bin/chmod", $"+x \"{path}\"");
            chmod.WaitForExit();
            if (chmod.ExitCode != 0)
            {
                throw new BuildFailedException($"[UUAV] chmod +x failed for {path}");
            }
        }
    }
}
