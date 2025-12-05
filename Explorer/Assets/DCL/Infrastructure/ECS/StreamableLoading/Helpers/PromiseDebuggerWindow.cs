using System;
using UnityEditor;
using UnityEngine;
using Arch.Core;
using System.Linq;
using ECS.StreamableLoading.Common.Components;
using ECS.Prioritization.Components;

namespace DCL.Infrastructure.Ecs.StreamableLoading
{
    public class PromiseDebuggerWindow : EditorWindow
    {
        [MenuItem("Decentraland/Debugger/Promise Pipeline")]
        public static void ShowWindow()
        {
            GetWindow<PromiseDebuggerWindow>("Promise Pipeline");
        }

        private Vector2 scrollPos;
        private string filter = "";
        private bool showFinished = true; // CHANGED: Default to true so you see data immediately
        private int totalScanned  ;
        private int totalVisible  ;

        private void OnGUI()
        {
            // 1. Toolbar
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            filter = EditorGUILayout.TextField("Filter URL/Name", filter, EditorStyles.toolbarTextField);
            showFinished = EditorGUILayout.ToggleLeft("Show Finished", showFinished, GUILayout.Width(100));
            if (GUILayout.Button("Force Repaint", EditorStyles.toolbarButton)) Repaint();
            GUILayout.EndHorizontal();

            // 2. Diagnostics Header
            EditorGUILayout.HelpBox($"Debug Status: Game Running: {Application.isPlaying} | Scanned Entities: {totalScanned} | Visible Rows: {totalVisible}", MessageType.Info);

            if (!Application.isPlaying) return;

            // 3. Table Headers
            GUILayout.BeginHorizontal("box");
            GUILayout.Label("Entity", GUILayout.Width(60));
            GUILayout.Label("State", GUILayout.Width(80));
            GUILayout.Label("Bucket", GUILayout.Width(50));
            GUILayout.Label("Intention / URL", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            // 4. Content
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            try
            {
                totalScanned = 0;
                totalVisible = 0;

                if (World.Worlds != null)
                {
                    // Convert to array to prevent collection modification errors during iteration
                    var worlds = World.Worlds.ToArray();
                    foreach (var world in worlds)
                    {
                        if (world == null) continue;
                        DrawWorldPromises(world);
                    }
                }
            }
            catch (Exception e)
            {
                EditorGUILayout.LabelField($"Error: {e.Message}");
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawWorldPromises(World world)
        {
            var query = new QueryDescription().WithAll<StreamableLoadingState>();

            // Count entities first to verify query is working
            totalScanned += world.CountEntities(in query);

            world.Query(in query, (Entity entity, ref StreamableLoadingState state) =>
            {
                // 1. Filter Logic
                if (!showFinished && state.Value == StreamableLoadingState.Status.Finished) return;

                // 2. Get Priority
                string bucketInfo = "-";
                if (world.TryGet(entity, out PartitionComponent partition))
                {
                    bucketInfo = partition.Bucket.ToString();
                    if (partition.Bucket == 0) bucketInfo = $"<color=green>{bucketInfo}</color>";
                }

                // 3. Find URL (Brute Force Component Search)
                string intentionInfo = "Unknown Intention";

                // Arch's GetAllComponents returns an array of object.
                // We iterate to find the Intention interface.
                object[] components = world.GetAllComponents(entity);

                if (components != null)
                {
                    foreach (object comp in components)
                    {
                        // Check if this component is the Intention
                        if (comp is ILoadingIntention intention)
                        {
                            string url = intention.CommonArguments.URL.ToString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                intentionInfo = url;
                                break;
                            }
                        }
                    }
                }

                // 4. Text Filter
                if (!string.IsNullOrEmpty(filter) && !intentionInfo.ToLower().Contains(filter.ToLower())) return;

                totalVisible++;
                DrawRow(entity, state.Value, bucketInfo, intentionInfo);
            });
        }

        private void DrawRow(Entity entity, StreamableLoadingState.Status status, string bucket, string info)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.TextField(entity.Id.ToString(), GUILayout.Width(60)); // TextField makes it copyable

            var statusStyle = new GUIStyle(EditorStyles.label);
            switch (status)
            {
                case StreamableLoadingState.Status.NotStarted: statusStyle.normal.textColor = Color.gray; break;
                case StreamableLoadingState.Status.InProgress: statusStyle.normal.textColor = Color.yellow; break;
                case StreamableLoadingState.Status.Allowed: statusStyle.normal.textColor = Color.cyan; break;
                case StreamableLoadingState.Status.Finished: statusStyle.normal.textColor = Color.green; break;
                case StreamableLoadingState.Status.Forbidden: statusStyle.normal.textColor = Color.red; break;
            }

            EditorGUILayout.LabelField(status.ToString(), statusStyle, GUILayout.Width(80));

            var bucketStyle = new GUIStyle(EditorStyles.label);
            bucketStyle.richText = true;
            EditorGUILayout.LabelField(bucket, bucketStyle, GUILayout.Width(50));

            EditorGUILayout.LabelField(info);

            EditorGUILayout.EndHorizontal();
        }
    }
}