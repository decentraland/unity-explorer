using Arch.Core;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace ECS.Editor.GUI.Windows
{
    public class EcsHierarchyWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset mVisualTreeAsset = default;

        [MenuItem("ECS/Hierarchy")]
        public static void ShowHierarchy()
        {
            EcsHierarchyWindow wnd = GetWindow<EcsHierarchyWindow>();
            wnd.titleContent = new GUIContent("Hierarchy");
        }

        private static Dictionary<string, World> mWorld = null;

        public void CreateGUI()
        {
            rootVisualElement.Add(mVisualTreeAsset.Instantiate());
        }

        public void OnEnable()
        {
            EcsMonitor.Instance.OnUpdate += Tick;
        }

        public void OnDisable()
        {
            EcsMonitor.Instance.OnUpdate -= Tick;
        }

        private void Tick()
        {
            // World Should be Refreshed here
            // Concern: Large Hierarchy's will be too slow
        }
    }
}
