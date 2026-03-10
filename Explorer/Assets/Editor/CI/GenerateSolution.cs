using System;
using System.Reflection;
using UnityEditor;

namespace CI
{
    public static class GenerateSolution
    {
        public static void Run()
        {
            AssetDatabase.Refresh();

            if (EditorUtility.scriptCompilationFailed)
                throw new Exception("Script compilation failed before generating the solution.");

            var editorAssembly = typeof(Editor).Assembly;
            var syncVsType = editorAssembly.GetType("UnityEditor.SyncVS");
            var syncSolutionMethod = syncVsType?.GetMethod("SyncSolution", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (syncSolutionMethod == null)
                throw new Exception("UnityEditor.SyncVS.SyncSolution was not found.");

            syncSolutionMethod.Invoke(null, null);

            AssetDatabase.Refresh();

            if (EditorUtility.scriptCompilationFailed)
                throw new Exception("Script compilation failed after generating the solution.");
        }
    }
}