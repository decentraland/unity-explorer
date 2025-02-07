using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Plugins.TexturesFuse.Editor
{
    public sealed class TexturesFuseBuildProcess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
                return;

            string projectDir = Path.GetDirectoryName(Application.dataPath);
            string sourceDir = Path.Combine(projectDir, "plugins");

            if (!Directory.Exists(sourceDir))
            {
                Debug.LogWarning(
                    $"Could not copy the texture fuse plugins to the build output because the source directory '{sourceDir}' does not exist.");

                return;
            }

            // Get the build path
            string outputDir = Path.GetDirectoryName(report.summary.outputPath);
            string targetDir = Path.Combine(outputDir, "plugins");

            CopyFilesRecursively(sourceDir, targetDir);
        }

        private static void CopyFilesRecursively(string sourceDir, string targetDir)
        {
            string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

            foreach (string sourceFile in files)
            {
                string targetFile = targetDir + sourceFile[sourceDir.Length..];
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(sourceFile, targetFile, overwrite: true);
            }
        }
    }
}
