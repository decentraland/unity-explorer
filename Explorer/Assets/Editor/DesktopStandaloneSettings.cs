using UnityEditor;

namespace Editor
{
    internal static class DesktopStandaloneSettings
    {
        private static readonly string SETTING_COPY_PDB_FILES = "CopyPDBFiles";

        internal static string platformName => "Standalone";

        internal static bool CopyPDBFiles
        {
            get => EditorUserBuildSettings.GetPlatformSettings(platformName, SETTING_COPY_PDB_FILES).ToLower() == "true";
            set => EditorUserBuildSettings.SetPlatformSettings(platformName, SETTING_COPY_PDB_FILES, value.ToString().ToLower());
        }
    }
}
