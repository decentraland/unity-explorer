using UnityEditor;

namespace DCL.PluginSystem.Editor
{
    public abstract class PluginSettingsEditorWindow : EditorWindow
    {
        public class Global : PluginSettingsEditorWindow
        {
            protected override PluginSettingsContainer Container => ContainerReference.GetGlobalContainer();
        }

        public class World : PluginSettingsEditorWindow
        {
            protected override PluginSettingsContainer Container => ContainerReference.GetWorldContainer();
        }

        protected abstract PluginSettingsContainer Container { get; }

        private UnityEditor.Editor assetEditor;

        [MenuItem("Decentraland/Global Plugins", priority = 10)]
        public static void ShowGlobalWindow()
        {
            GetWindow<Global>("Global Plugins");
        }

        [MenuItem("Decentraland/World Plugins", priority = 10)]
        public static void ShowWorldWindow()
        {
            GetWindow<World>("World Plugins");
        }

        private void OnEnable()
        {
            assetEditor = UnityEditor.Editor.CreateEditor(Container);
        }

        private void OnDisable()
        {
            DestroyImmediate(assetEditor);
        }

        private void CreateGUI()
        {
            rootVisualElement.Add(assetEditor.CreateInspectorGUI());
        }
    }
}
