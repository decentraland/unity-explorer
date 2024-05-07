using EntitiesDebugTool.Runtime.Hub;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EntitiesDebugTool.Editor
{
    public class EntitiesEditorWindow : EditorWindow
    {
        private readonly ISnapshotsHub hub = new ISnapshotsHub.Fake();
        private string currentSelected = string.Empty;
        private int currentIndex;

        [MenuItem("Arch/View/Entities")]
        private static void ShowWindow()
        {
            var window = GetWindow<EntitiesEditorWindow>()!;
            window.titleContent = new GUIContent("Entities Debug Window");
            window.Show();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Take Snapshot"))
                hub.Snapshot(currentSelected).TakeSnapshot();

            string[] arrayWorlds = hub.AvailableWorlds().ToArray();

            if (arrayWorlds.Length == 0)
            {
                GUILayout.Label($"No available worlds found");
                return;
            }

            currentIndex = EditorGUILayout.Popup(currentIndex, arrayWorlds);
            currentSelected = arrayWorlds[currentIndex];

            var entities = hub.Snapshot(currentSelected).Entities();

            GUILayout.Label(entities is null ? "Snapshot is not taken yet" : $"Entities count: {entities.Count}");

            foreach ((int id, object[] objects) in entities ?? Array.Empty<(int id, object[])>())
            {
                GUILayout.Label($"Entity Id: {id}", new GUIStyle { fontStyle = FontStyle.Bold });

                foreach (object o in objects)
                    GUILayout.Label($"Component: {o.GetType().Name}, content: {o}");
            }
        }
    }
}
