using UnityEditor;
using UnityEditor.Compilation;

namespace CI
{
    public static class GenerateSolution
    {
        public static void Run()
        {
            AssetDatabase.Refresh();

            if (EditorUtility.scriptCompilationFailed)
                throw new BuildFailedException("Script compilation failed before generating the solution.");

            SyncVS.SyncSolution();
            AssetDatabase.Refresh();

            if (EditorUtility.scriptCompilationFailed)
                throw new BuildFailedException("Script compilation failed after generating the solution.");
        }
    }
}
