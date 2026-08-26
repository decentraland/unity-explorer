using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
    internal sealed class VCRedistBuildPostprocessor : IPostprocessBuildWithReport
    {
        private const string SOURCE_DIRECTORY = "Assets/Plugins/.VCRedist/x64";
        private const string DOCS = "docs/build-and-ci.md";

        private static readonly string[] RUNTIME_DLLS =
        {
            "msvcp140.dll",
            "vcruntime140.dll",
            "vcruntime140_1.dll",
        };

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
                return;

            var buildRoot = Path.GetDirectoryName(report.summary.outputPath)!;

            foreach (string dll in RUNTIME_DLLS)
                Copy(Path.GetFullPath($"{SOURCE_DIRECTORY}/{dll}"), Path.Combine(buildRoot, dll));
        }

        private static void Copy(string source, string destination)
        {
            if (File.Exists(source) == false)
                throw new BuildFailedException($"[VCRedist] {source} is missing; see {DOCS}");

            File.Copy(source, destination, overwrite: true);
            Debug.Log($"[VCRedist] deployed {destination}");
        }
    }
}