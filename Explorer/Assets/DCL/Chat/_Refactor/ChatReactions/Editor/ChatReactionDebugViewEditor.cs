#if UNITY_EDITOR
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Chat.ChatReactions.Debug;
using DCL.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Editor
{
    [CustomEditor(typeof(ChatReactionDebugView))]
    public sealed class ChatReactionDebugViewEditor : UnityEditor.Editor
    {
        private const int WALLET_PREVIEW_LENGTH = 10;
        private const int MESSAGE_ID_PREVIEW_LENGTH = 8;

        private Vector2 sentScrollPos;
        private Vector2 receivedScrollPos;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var view = (ChatReactionDebugView)target;

            if (!view.Initialized)
            {
                EditorGUILayout.HelpBox("Not initialized. Enter Play Mode to activate.", MessageType.Info);
                return;
            }

            var config = view.Config;
            var stats = view.LastStats;

            DrawDebugToggleButton(config);
            DrawLiveStats(stats, config);
            DrawRecents();
            DrawDebugToggles(config);
            DrawConfigReferences(config);
            DrawSentLog(view);
            DrawReceivedLog(view);
        }

        private static void DrawLiveStats(ChatReactionStats stats, ChatReactionsConfig? config)
        {
            EditorGUILayout.LabelField("Live Stats", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("UI Lane");
            EditorGUI.indentLevel++;
            Label("Alive", $"{stats.UIAliveCount} / {stats.UIPoolCapacity}");
            if (config != null)
                Label("Max Visible", config.UILane.MaxVisibleParticles == 0 ? "unlimited" : config.UILane.MaxVisibleParticles.ToString());
            Label("Streaming", StateText(stats.IsUIStreaming));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("World Lane");
            EditorGUI.indentLevel++;
            Label("Alive", $"{stats.WorldAliveCount} / {stats.WorldPoolCapacity}");
            Label("Visible", $"{stats.WorldVisibleCount}  (anchors: {stats.WorldVisibleAnchors})");
            if (config != null)
            {
                Label("Max Per Avatar", config.WorldLane.MaxParticlesPerAvatar == 0 ? "unlimited" : config.WorldLane.MaxParticlesPerAvatar.ToString());
                Label("Max Spawn Distance", $"{config.WorldLane.MaxSpawnDistance} units");
            }
            Label("Local Anchor Alive", stats.LocalAnchorAlive < 0 ? "no anchor" : stats.LocalAnchorAlive.ToString());
            Label("Dropped (pool full)", stats.DroppedThisFrame.ToString());
            Label("Capped (per-avatar)", stats.CappedThisFrame.ToString());
            Label("Streaming", StateText(stats.IsWorldStreaming));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Avatars");
            EditorGUI.indentLevel++;
            Label("Nearby", stats.NearbyAvatarCount.ToString());
            Label("Debug Nearby", StateText(stats.IsDebugNearbyActive));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Anchor Table");
            EditorGUI.indentLevel++;
            Label("Active Anchors", $"{stats.ActiveAnchorCount} / {stats.AnchorSlotCapacity}");
            Label("Scan Limit", stats.AnchorScanLimit.ToString());
            EditorGUI.indentLevel--;

            DrawSeparator();
        }

        private static void DrawRecents()
        {
            var service = ChatReactionRecentsService.Current;

            EditorGUILayout.LabelField("Recents", EditorStyles.boldLabel);

            if (service == null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("(service not available)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                DrawSeparator();
                return;
            }

            EditorGUI.indentLevel++;
            Label("Recents", service.Recents.Count.ToString());
            Label("Dirty", service.IsDirty ? "[Yes]" : "[No]");

            if (service.Recents.Count > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Shortcuts Bar (most recent first)", EditorStyles.miniBoldLabel);

                for (int i = 0; i < service.Recents.Count; i++)
                    EditorGUILayout.LabelField($"  [{i}] atlasIndex={service.Recents[i]}", EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear Recents"))
            {
                service.ClearAll();
                ReportHub.Log(ReportCategory.CHAT_MESSAGES,"[ChatReactions] Recents cleared.");
            }

            if (service.IsDirty && GUILayout.Button("Flush to Disk"))
            {
                service.FlushIfDirty();
                ReportHub.Log(ReportCategory.CHAT_MESSAGES,"[ChatReactions] Recents flushed to disk.");
            }

            EditorGUILayout.EndHorizontal();

            DrawSeparator();
        }

        private static void DrawDebugToggles(ChatReactionsConfig? config)
        {
            if (config == null) return;

            EditorGUILayout.LabelField("Debug Toggles", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            Label("Debug Enabled", StateText(config.DebugEnabled));
            Label("Stream UI Lane", StateText(config.StreamUILane));
            Label("Stream Local Player", StateText(config.StreamLocalPlayer));
            Label("Stream Remote Players", StateText(config.StreamRemotePlayers));


            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Message Reactions", EditorStyles.miniBoldLabel);
            Label("Tooltip Mock Users", StateText(config.MessageReactions.TooltipMockUsersEnabled));
            Label("Tooltip Mock Loading", StateText(config.MessageReactions.TooltipMockLoadingEnabled));
            EditorGUI.indentLevel--;

            DrawSeparator();
        }

        private static void DrawConfigReferences(ChatReactionsConfig? config)
        {
            if (config == null) return;

            EditorGUILayout.LabelField("Configs (click to select)", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            DrawObjectField("Reactions Config", config);
            DrawObjectField("UI Lane", config.UILane);
            DrawObjectField("World Lane", config.WorldLane);
            DrawObjectField("Message Config", config.MessageReactions);
            EditorGUI.indentLevel--;

            DrawSeparator();
        }

        private void DrawSentLog(ChatReactionDebugView view)
        {
            EditorGUILayout.LabelField($"Sent Log ({view.SentLog.Count})", EditorStyles.boldLabel);

            if (view.SentLog.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                DrawSeparator();
                return;
            }

            sentScrollPos = EditorGUILayout.BeginScrollView(sentScrollPos, GUILayout.MaxHeight(150));
            EditorGUI.indentLevel++;

            foreach (var e in view.SentLog)
            {
                if (e.EmojiIndex < 0)
                    EditorGUILayout.LabelField($"[{e.Timestamp:F2}s] FLUSH  total={e.Count}", EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField($"[{e.Timestamp:F2}s] (local)  emoji={e.EmojiIndex}  count={e.Count}  type={e.Type}", EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndScrollView();

            DrawSeparator();
        }

        private void DrawReceivedLog(ChatReactionDebugView view)
        {
            EditorGUILayout.LabelField($"Received Log ({view.ReceivedLog.Count})", EditorStyles.boldLabel);

            if (view.ReceivedLog.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            receivedScrollPos = EditorGUILayout.BeginScrollView(receivedScrollPos, GUILayout.MaxHeight(150));
            EditorGUI.indentLevel++;

            foreach (var e in view.ReceivedLog)
            {
                string wallet = Truncate(e.WalletId, WALLET_PREVIEW_LENGTH);
                string line = $"[{wallet}]  emoji={e.EmojiIndex}  count={e.Count}  type={e.Type}";

                if (!string.IsNullOrEmpty(e.MessageId))
                    line += $"  msgId={Truncate(e.MessageId, MESSAGE_ID_PREVIEW_LENGTH)}";

                if (e.IsRemoval)
                    line += "  REMOVE";

                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndScrollView();
        }

        private static void DrawDebugToggleButton(ChatReactionsConfig? config)
        {
            if (config == null) return;

            string label = config.DebugEnabled ? "Disable Debug Mode" : "Enable Debug Mode";
            var color = GUI.backgroundColor;
            GUI.backgroundColor = config.DebugEnabled ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);

            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                config.DebugEnabled = !config.DebugEnabled;
                EditorUtility.SetDirty(config);
            }

            GUI.backgroundColor = color;
            EditorGUILayout.Space(4);
        }

        // --- Helpers ---

        private static void Label(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        private static string StateText(bool enabled) =>
            enabled ? "[Enabled]" : "[Disabled]";

        private static void DrawObjectField(string label, Object? obj)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            if (obj != null && GUILayout.Button(obj.name, EditorStyles.linkLabel))
                EditorGUIUtility.PingObject(obj);

            EditorGUILayout.EndHorizontal();
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "(none)";

            return value.Length <= maxLength ? value : value[..maxLength] + "..";
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
            EditorGUILayout.Space(2);
        }
    }
}
#endif
