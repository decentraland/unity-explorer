using UnityEditor;

namespace Editor
{
    internal static class DesktopStandaloneSettings
    {
        private const string SETTING_COPY_PDB_FILES = "CopyPDBFiles";

        private static string PlatformName => "Standalone";

        internal static bool CopyPDBFiles
        {
            get => EditorUserBuildSettings.GetPlatformSettings(platformName, SETTING_COPY_PDB_FILES).ToLower() == "true";
            set => EditorUserBuildSettings.SetPlatformSettings(platformName, SETTING_COPY_PDB_FILES, value.ToString().ToLower());
        }
    }
}
