using UnityEditor;

namespace DCL.PluginSystem.Editor
{
    public static class ContainerReference
    {
        private const string GLOBAL_GUID = "7575a56449e4af444b9c13267319c95d";
        private const string WORLD_GUID = "52a2a6abe420a8b4db9dc55df4b42e92";

        private static GlobalPluginSettingsContainer globalContainer;
        private static WorldPluginSettingsContainer worldContainer;

        public static GlobalPluginSettingsContainer GetGlobalContainer()
        {
            return globalContainer ??= GetContainer<GlobalPluginSettingsContainer>(GLOBAL_GUID);
        }

        public static WorldPluginSettingsContainer GetWorldContainer()
        {
            return worldContainer ??= GetContainer<WorldPluginSettingsContainer>(WORLD_GUID);
        }

        private static T GetContainer<T>(string guid) where T : PluginSettingsContainer =>
            AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    }
}
