using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;

namespace Plugins.TexturesFuse.Editor
{
    public class TexturesFuseBuildProcess : IPreprocessBuildWithReport
    {
        private const string PATH_TO_MODIFY = "Assets/Plugins/TexturesFuse/textures-server";

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
    }
}
