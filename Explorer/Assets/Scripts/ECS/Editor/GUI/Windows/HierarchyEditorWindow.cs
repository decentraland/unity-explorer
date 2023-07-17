using Arch.Core;
using ECS.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tree = Unity.VisualScripting.Antlr3.Runtime.Tree.Tree;

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

        var treeView = rootVisualElement.GetFirstAncestorOfType<TreeView>();
        treeView.makeItem
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
