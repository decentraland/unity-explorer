using UnityEditor;

namespace DCL.PluginSystem.Editor
{
    public class PluginSettingsEditorWindow : EditorWindow
    {
        private UnityEditor.Editor assetEditor;

        [MenuItem("Decentraland/Plugin Settings", priority = 10)]
        public static void ShowWindow()
        {
            GetWindow<PluginSettingsEditorWindow>("Plugin Settings");
        }

        private void OnEnable()
        {
            assetEditor = UnityEditor.Editor.CreateEditor(ContainerReference.GetContainer());
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
