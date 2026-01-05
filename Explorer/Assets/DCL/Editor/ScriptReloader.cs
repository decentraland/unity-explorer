using UnityEditor;

namespace DCL.Editor
{
    public static class ScriptReloader
    {
        [MenuItem("Decentraland/Reload Scripts")]
        private static void ReloadScripts() =>
            EditorUtility.RequestScriptReload();
    }
}
