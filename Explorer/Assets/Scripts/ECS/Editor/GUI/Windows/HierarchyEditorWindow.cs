using Arch.Core;
using ECS.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class HierarchyEditorWindow : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("ECS/Hierarchy")]
    public static void ShowExample()
    {
        HierarchyEditorWindow wnd = GetWindow<HierarchyEditorWindow>();
        wnd.titleContent = new GUIContent("Hierarchy");
    }

    public void CreateGUI()
    {
        rootVisualElement.Add(m_VisualTreeAsset.Instantiate());
    }

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
        }
    }
}
