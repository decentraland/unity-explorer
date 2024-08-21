using System.Collections.Generic;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace DCL.Profiling.Editor
{
    public class ProfilerMarkerViewer : EditorWindow
    {
        private readonly List<ProfilerRecorderHandle> availableStatHandles = new ();
        private readonly Dictionary<string, List<ProfilerRecorderDescription>> groupedStats = new ();

        private readonly List<string> categories = new ();
        private readonly List<string> flags = new ();
        private HashSet<string> selectedCategories = new ();
        private HashSet<string> selectedFlags = new ();

        private Vector2 scrollPosition;
        private string searchString = string.Empty;
        private float sidebarWidth = 250f;
        private Rect sidebarRect;
        private Rect mainRect;
        private bool isResizing;

        private Vector2 mainScrollPosition;

        private void OnEnable()
        {
            LoadProfilerMarkers();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            // Sidebar
            sidebarRect = new Rect(0, 0, sidebarWidth, position.height);
            GUILayout.BeginArea(sidebarRect);
            DrawSidebar();
            GUILayout.EndArea();

            // Resizer
            EditorGUILayout.BeginVertical(GUILayout.Width(5));
            DrawResizer();
            EditorGUILayout.EndVertical();

            // Main content
            mainRect = new Rect(sidebarWidth + 5, 0, position.width - sidebarWidth - 5, position.height);
            GUILayout.BeginArea(mainRect);
            DrawMainContent();
            GUILayout.EndArea();

            EditorGUILayout.EndHorizontal();
        }

        [MenuItem("Decentraland/Profiling/Profiler Marker Viewer")]
        private static void ShowWindow()
        {
            ProfilerMarkerViewer window = GetWindow<ProfilerMarkerViewer>();
            window.titleContent = new GUIContent("Profiler Marker Viewer");
            window.Show();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(sidebarWidth));

            if (GUILayout.Button("Refresh"))
                LoadProfilerMarkers();

            searchString = EditorGUILayout.TextField("Search", searchString);

            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawFilterSection("Categories", categories, selectedCategories);
            EditorGUILayout.Space();
            DrawFilterSection("Flags", flags, selectedFlags);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFilterSection(string title, List<string> items, HashSet<string> selectedItems)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("All", GUILayout.Width(50)))
            {
                selectedItems.Clear();
                selectedItems.UnionWith(items);
            }

            if (GUILayout.Button("None", GUILayout.Width(50)))
                selectedItems.Clear();

            EditorGUILayout.EndHorizontal();

            foreach (string item in items)
            {
                bool isSelected = selectedItems.Contains(item);
                bool newIsSelected = EditorGUILayout.ToggleLeft(item, isSelected);

                if (newIsSelected == isSelected) continue;

                if (newIsSelected)
                    selectedItems.Add(item);
                else
                    selectedItems.Remove(item);
            }
        }

        private void DrawMainContent()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Filtered Markers", EditorStyles.boldLabel);

            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

            foreach (string category in categories)
            {
                if (!selectedCategories.Contains(category)) continue;

                foreach (ProfilerRecorderDescription description in groupedStats[category])
                {
                    if (!selectedFlags.Contains(description.Flags.ToString())) continue;
                    if (!string.IsNullOrEmpty(searchString) && !description.Name.ToLower().Contains(searchString.ToLower())) continue;

                    EditorGUILayout.LabelField($"[{category.ToUpper()}] {description.Name} | <{description.Flags}>");
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawResizer()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            Rect resizerRect = GUILayoutUtility.GetRect(5f, 5f, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(resizerRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && resizerRect.Contains(Event.current.mousePosition)) { isResizing = true; }
            else if (Event.current.type == EventType.MouseUp) { isResizing = false; }

            if (isResizing)
            {
                sidebarWidth = Mathf.Clamp(Event.current.mousePosition.x, 100, position.width - 100);
                Repaint();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void LoadProfilerMarkers()
        {
            availableStatHandles.Clear();
            groupedStats.Clear();
            categories.Clear();
            flags.Clear();

            ProfilerRecorderHandle.GetAvailable(availableStatHandles);

            foreach (ProfilerRecorderHandle handle in availableStatHandles)
            {
                ProfilerRecorderDescription statDescription = ProfilerRecorderHandle.GetDescription(handle);

                string category = statDescription.Category.Name;
                var flag = statDescription.Flags.ToString();

                if (!groupedStats.ContainsKey(category))
                {
                    groupedStats[category] = new List<ProfilerRecorderDescription>();
                    categories.Add(category);
                }

                groupedStats[category].Add(statDescription);

                if (!flags.Contains(flag))
                    flags.Add(flag);
            }

            categories.Sort();
            flags.Sort();

            selectedCategories = new HashSet<string>(categories);
            selectedFlags = new HashSet<string>(flags);
        }

        [MenuItem("Decentraland/Profiling/Print All Profiler Markers")]
        private static void PrintAllProfilerMarkers()
        {
            var availableStatHandles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(availableStatHandles);

            var groupedStats = new Dictionary<string, List<string>>();

            foreach (ProfilerRecorderHandle handle in availableStatHandles)
            {
                ProfilerRecorderDescription statDescription = ProfilerRecorderHandle.GetDescription(handle);

                string category = statDescription.Category.Name;

                if (!groupedStats.ContainsKey(category))
                    groupedStats[category] = new List<string>();

                groupedStats[category].Add($"{statDescription.Name} | <{statDescription.Flags.ToString()}>");
            }

            foreach (string category in groupedStats.Keys)
            foreach (string stat in groupedStats[category])
                Debug.Log($"[{category.ToUpper()}] | {stat}");
        }
    }
}
