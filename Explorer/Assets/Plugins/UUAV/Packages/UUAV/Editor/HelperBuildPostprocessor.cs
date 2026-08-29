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
    /// Unity copies only recognized plugin files (.dll/.dylib/.so) out of
    /// Plugins folders; the helper executable — and, on Linux, the versioned
    /// FFmpeg sonames (whose extensions Unity's importer does not
    /// recognize) — import as DefaultAssets and must be placed next to the
    /// deployed uuav plugin manually, where the client library resolves them
    /// (and where the FFmpeg libraries resolve for the helper itself).
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
                case BuildTarget.StandaloneLinux64:
                    CopyLinuxHelperAndLibs(report.summary.outputPath);
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

        // The helper and the versioned FFmpeg sonames are DefaultAssets
        // Unity never deploys; they must land in the same folder Unity copied
        // libuuav.so into, so the client's $ORIGIN runpath resolves them.
        // Locate that folder by finding the deployed libuuav.so rather than
        // hardcoding a layout that a Unity version could change.
        private static void CopyLinuxHelperAndLibs(string exePath)
        {
            var dataDirectory = Path.Combine(
                Path.GetDirectoryName(exePath)!,
                Path.GetFileNameWithoutExtension(exePath) + "_Data"
            );
            var pluginsRoot = Path.Combine(dataDirectory, "Plugins");
            var deployed = Directory.Exists(pluginsRoot)
                ? Directory.GetFiles(pluginsRoot, "libuuav.so", SearchOption.AllDirectories)
                : System.Array.Empty<string>();
            if (deployed.Length == 0)
            {
                throw new BuildFailedException(
                    $"[UUAV] libuuav.so was not deployed under {pluginsRoot}; is the Linux plugin enabled for this build?"
                );
            }
            var destinationDir = Path.GetDirectoryName(deployed[0])!;

            var source = Path.GetFullPath($"{PACKAGE_PLUGINS}/linux-x86_64");
            var helper = Path.Combine(destinationDir, "uuav-helper");
            Copy(Path.Combine(source, "uuav-helper"), helper);
            MakeExecutable(helper);

            // every lib*.so.* soname (the FFmpeg libraries; libva is
            // host-provided, not shipped); libuuav.so /
            // libuuav_core.so are real plugins Unity already deployed
            foreach (var lib in Directory.GetFiles(source, "lib*.so.*"))
            {
                if (lib.EndsWith(".meta"))
                {
                    continue;
                }
                Copy(lib, Path.Combine(destinationDir, Path.GetFileName(lib)));
            }
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
        // client library can spawn the helper from inside the build. Only
        // meaningful when /bin/chmod exists (macOS/Linux editors).
        private static void MakeExecutable(string path)
        {
            if (Application.platform != RuntimePlatform.OSXEditor
                && Application.platform != RuntimePlatform.LinuxEditor)
            {
                Debug.LogWarning(
                    $"[UUAV] cannot set the executable bit on {path} from this OS; fix it before shipping the build"
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
