using UnityEditor;

namespace Editor
{
    internal static class DesktopStandaloneSettings
    {
        private const string SETTING_COPY_PDB_FILES = "CopyPDBFiles";

        private static string PlatformName => "Standalone";

        internal static bool CopyPDBFiles
        {
            get => EditorUserBuildSettings.GetPlatformSettings(PlatformName, SETTING_COPY_PDB_FILES).ToLower() == "true";
            set => EditorUserBuildSettings.SetPlatformSettings(PlatformName, SETTING_COPY_PDB_FILES, value.ToString().ToLower());
        }
    }
}
