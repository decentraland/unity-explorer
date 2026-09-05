using UnityEditor;
using UnityEngine.UIElements;

namespace DCL.Gizmos.Editor
{
    [CustomEditor(typeof(DrawSceneGizmosHub))]
    public class DrawSceneGizmosHubEditor : UnityEditor.Editor
    {
        private DrawSceneGizmosHub hub;

        private void OnEnable()
        {
            hub = (DrawSceneGizmosHub)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            for (var index = 0; index < hub.gizmosProviders.Length; index++)
            {
                int i = index;
                DrawSceneGizmosHub.ProviderState state = hub.gizmosProviders[i];

                var toggle = new Toggle(state.gizmosProvider.name);
                toggle.value = state.active;

                toggle.RegisterValueChangedCallback(evt => hub.SetGizmosActive(i, evt.newValue));

                root.Add(toggle);
            }

            return root;
        }
    }
}
