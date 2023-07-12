using Arch.Core;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ECS.Editor.GUI
{
 public class HierarchyEditorWindow : EditorWindow
    {
        [MenuItem("ECS/Hierarchy")]
        public static void ShowExample()
        {
            HierarchyEditorWindow wnd = GetWindow<HierarchyEditorWindow>();
            wnd.titleContent = new GUIContent("ECS Hierarchy");
        }

        Dictionary<string, Label> labels = new ();

        public void OnEnable()
        {
            EditorSceneMonitor.Instance.OnUpdate += Tick;
        }

        public void OnDisable()
        {
            EditorSceneMonitor.Instance.OnUpdate -= Tick;
        }

        private void Tick()
        {
            var scenes = EditorSceneMonitor.Instance.GetScenes();
            foreach(KeyValuePair<string, World> scene in scenes)
            {
                if (labels.ContainsKey(scene.Key)) continue;
                Label label = new Label(scene.Key);
                labels.Add(scene.Key, label);
                rootVisualElement.Add(label);
            }
        }
    }
}
