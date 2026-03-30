#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Editor
{
    public class ChatReactionStatsWindow : EditorWindow
    {
        [MenuItem("Decentraland/Debug/Chat Reaction Stats")]
        public static void Open() => GetWindow<ChatReactionStatsWindow>("Reaction Stats");

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            ChatReactionDebugState? state = ChatReactionDebugState.Current;

            if (!Application.isPlaying || state == null)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live stats.", MessageType.Info);
                return;
            }

            ChatReactionStats s = state.LastStats;

            EditorGUILayout.LabelField("UI Lane", EditorStyles.boldLabel);
            DrawReadOnly("Alive", s.UIAliveCount);
            DrawReadOnly("Pool Capacity", s.UIPoolCapacity);
            DrawReadOnly("Streaming", s.IsUIStreaming);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("World Lane", EditorStyles.boldLabel);
            DrawReadOnly("Alive", s.WorldAliveCount);
            DrawReadOnly("Visible", s.WorldVisibleCount);
            DrawReadOnly("Visible Anchors", s.WorldVisibleAnchors);
            DrawReadOnly("Pool Capacity", s.WorldPoolCapacity);
            DrawReadOnly("Streaming", s.IsWorldStreaming);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Avatars", EditorStyles.boldLabel);
            DrawReadOnly("Nearby Count", s.NearbyAvatarCount);
            DrawReadOnly("Debug Nearby Active", s.IsDebugNearbyActive);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Anchor Table", EditorStyles.boldLabel);
            DrawReadOnly("Active Anchors", s.ActiveAnchorCount);
            DrawReadOnly("Scan Limit", s.AnchorScanLimit);
            DrawReadOnly("Slot Capacity", s.AnchorSlotCapacity);
        }

        private static void DrawReadOnly(string label, int value)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField(label, value);
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawReadOnly(string label, bool value)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle(label, value);
            EditorGUI.EndDisabledGroup();
        }
    }
}
#endif
