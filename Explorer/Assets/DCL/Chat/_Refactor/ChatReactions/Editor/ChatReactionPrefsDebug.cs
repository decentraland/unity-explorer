#if UNITY_EDITOR
using DCL.Diagnostics;
using DCL.Prefs;
using UnityEditor;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Editor
{
    public static class ChatReactionPrefsDebug
    {
        [MenuItem("Decentraland/Cache/Clear Reaction Recents")]
        public static void ClearReactionRecents()
        {
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.CHAT_REACTION_FAVORITES, save: true);
            ReportHub.Log(ReportCategory.CHAT_MESSAGES,"[ChatReactions] Cleared reaction recents. Restart Play Mode for the change to take effect.");
        }

        [MenuItem("Decentraland/Cache/Clear Reaction Recents", validate = true)]
        public static bool ValidateClearReactionRecents() => Application.isPlaying;

        [MenuItem("Decentraland/Cache/Log Reaction Recents")]
        public static void LogReactionRecents()
        {
            if (!DCLPlayerPrefs.HasKey(DCLPrefKeys.CHAT_REACTION_FAVORITES))
            {
                ReportHub.Log(ReportCategory.CHAT_MESSAGES,"[ChatReactions] No saved reaction recents found in PlayerPrefs.");
                return;
            }

            string saved = DCLPlayerPrefs.GetString(DCLPrefKeys.CHAT_REACTION_FAVORITES);

            if (string.IsNullOrEmpty(saved))
            {
                ReportHub.Log(ReportCategory.CHAT_MESSAGES,"[ChatReactions] Reaction recents key exists but is empty.");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ChatReactions] Raw PlayerPrefs value: \"{saved}\"");
            sb.AppendLine("[ChatReactions] Parsed entries:");

            string[] entries = saved.Split(';');

            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];

                if (string.IsNullOrEmpty(entry))
                    continue;

                int colonIdx = entry.IndexOf(':');

                if (colonIdx > 0)
                    sb.AppendLine($"  [{i}] atlasIndex={entry.Substring(0, colonIdx)}, count={entry.Substring(colonIdx + 1)}");
                else
                    sb.AppendLine($"  [{i}] atlasIndex={entry} (legacy format, count=1)");
            }

            ReportHub.Log(ReportCategory.CHAT_MESSAGES,sb.ToString());
        }
    }
}
#endif
