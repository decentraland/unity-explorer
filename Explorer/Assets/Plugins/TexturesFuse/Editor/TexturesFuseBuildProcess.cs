using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Plugins.TexturesFuse.Editor
{
    public class TexturesFuseBuildProcess : IPostprocessBuildWithReport
    {
        private const string PLUGINS_DIR = "../TexturesFuse/plugins";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (Application.platform is not RuntimePlatform.WindowsEditor)
                return;

            // Get the build path
            string buildPath = report!.summary.outputPath!;
            string targetDir = Path.Combine(buildPath, "plugins");

            // Check if source folder exists
            if (Directory.Exists(PLUGINS_DIR) == false)
                Debug.LogError("Source folder does not exist. No files were copied.");

            CopyFilesRecursively(PLUGINS_DIR, targetDir);
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }
    }
}
