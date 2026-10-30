using UnityEditor;

namespace DCL.PluginSystem.Editor
{
    public static class ContainerReference
    {
        private const string GUID = "7575a56449e4af444b9c13267319c95d";

        private static PluginSettingsContainer container;

        public static PluginSettingsContainer GetContainer() =>
            container ??= AssetDatabase.LoadAssetAtPath<PluginSettingsContainer>(AssetDatabase.GUIDToAssetPath(GUID));
    }
}
