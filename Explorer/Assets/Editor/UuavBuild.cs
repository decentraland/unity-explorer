using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
    /// <summary>
    ///     Driven from the command line with <c>-executeMethod
    ///     Editor.UuavBuild.Windows64</c> (or <c>OSX</c>); exits the editor with the
    ///     build result so a shell can gate on it.
    /// </summary>
    public static class UuavBuild
    {
        public static void Windows64() => Run(BuildTarget.StandaloneWindows64, "../Builds/win64/Explorer.exe");

        public static void OSX() => Run(BuildTarget.StandaloneOSX, "../Builds/osx/Explorer.app");

        private static void Run(BuildTarget target, string path)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Debug.Log($"UUAVBUILD scenes={scenes.Length} target={target} path={path}");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.Development,
            });

            BuildSummary s = report.summary;
            Debug.Log($"UUAVBUILD RESULT={s.result} errors={s.totalErrors} warnings={s.totalWarnings} bytes={s.totalSize}");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
