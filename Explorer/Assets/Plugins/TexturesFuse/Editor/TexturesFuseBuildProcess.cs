using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Plugins.TexturesFuse.Editor
{
    public class TexturesFuseBuildProcess : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string PATH_TO_MODIFY = "Assets/Plugins/TexturesFuse/textures-server";
        private const string PLUGINS_DIR = "plugins";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var result = Directory.EnumerateFiles(PATH_TO_MODIFY, "*.h", SearchOption.AllDirectories)
                                  .Concat(Directory.EnumerateFiles(PATH_TO_MODIFY, "*.c", SearchOption.AllDirectories))
                                  .Concat(Directory.EnumerateFiles(PATH_TO_MODIFY, "*.cpp", SearchOption.AllDirectories));

            foreach (string filePath in result)
                File.Move(filePath, $"{filePath}_ignore");

            // Refresh the AssetDatabase to reflect changes
            AssetDatabase.Refresh();
        }

        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            var result = Directory.EnumerateFiles(PATH_TO_MODIFY, "*.h_ignore", SearchOption.AllDirectories)
                                  .Concat(Directory.EnumerateFiles(PATH_TO_MODIFY, "*.c_ignore", SearchOption.AllDirectories))
                                  .Concat(Directory.EnumerateFiles(PATH_TO_MODIFY, "*.cpp_ignore", SearchOption.AllDirectories));

            foreach (string filePath in result)
                File.Move(filePath, filePath.Replace("_ignore", ""));

            // Refresh the AssetDatabase to reflect changes
            AssetDatabase.Refresh();
        }

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
