using System.Collections.Generic;
using DCL.Prefs;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Tracks emoji usage frequency and exposes the top N most-used emojis
    /// (excluding fixed defaults) for the shortcuts bar. Persists via
    /// <see cref="DCLPlayerPrefs"/> in the format <c>index:count;index:count;...</c>.
    /// </summary>
    public sealed class ChatReactionRecentsService
    {
        private const char ENTRY_SEPARATOR = ';';
        private const char COUNT_SEPARATOR = ':';

        private readonly int[] fixedDefaults;
        private readonly int maxRecent;
        private readonly List<EmojiUsage> usageEntries = new();
        private readonly List<int> topRecents = new();
        private readonly System.Text.StringBuilder saveBuilder = new();

        public IReadOnlyList<int> Recents => topRecents;

        public ChatReactionRecentsService(int[] fixedDefaults, int maxRecent)
        {
            this.fixedDefaults = fixedDefaults;
            this.maxRecent = maxRecent;
            Load();
            RebuildTopRecents();
        }

        /// <summary>
        /// Increments the usage count for this emoji. Fixed defaults are ignored.
        /// Call this every time the user sends a non-default emoji.
        /// </summary>
        public void RecordUsage(int atlasIndex)
        {
            if (IsFixedDefault(atlasIndex))
                return;

            bool found = false;

            for (int i = 0; i < usageEntries.Count; i++)
            {
                if (usageEntries[i].AtlasIndex != atlasIndex) continue;

                var entry = usageEntries[i];
                entry.Count++;
                usageEntries[i] = entry;
                found = true;
                break;
            }

            if (!found)
                usageEntries.Add(new EmojiUsage(atlasIndex, 1));

            RebuildTopRecents();
            Save();
        }

        public bool IsFixedDefault(int atlasIndex)
        {
            for (int i = 0; i < fixedDefaults.Length; i++)
            {
                if (fixedDefaults[i] == atlasIndex)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Rebuilds <see cref="topRecents"/> from the top N entries sorted by count descending.
        /// Uses a simple selection approach since the list is tiny (typically &lt; 30 entries).
        /// </summary>
        private void RebuildTopRecents()
        {
            topRecents.Clear();

            int count = maxRecent < usageEntries.Count ? maxRecent : usageEntries.Count;

            // Simple insertion sort into topRecents — no allocations, tiny N.
            for (int pick = 0; pick < count; pick++)
            {
                int bestIdx = -1;
                int bestCount = -1;

                for (int i = 0; i < usageEntries.Count; i++)
                {
                    if (usageEntries[i].Count <= bestCount) continue;
                    if (topRecents.Contains(usageEntries[i].AtlasIndex)) continue;

                    bestIdx = i;
                    bestCount = usageEntries[i].Count;
                }

                if (bestIdx >= 0)
                    topRecents.Add(usageEntries[bestIdx].AtlasIndex);
            }
        }

        private void Load()
        {
            usageEntries.Clear();

            // One-time cleanup of the legacy selected-emoji pref key.
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.CHAT_REACTION_SELECTED))
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.CHAT_REACTION_SELECTED);

            if (!DCLPlayerPrefs.HasKey(DCLPrefKeys.CHAT_REACTION_FAVORITES))
                return;

            string saved = DCLPlayerPrefs.GetString(DCLPrefKeys.CHAT_REACTION_FAVORITES);

            if (string.IsNullOrEmpty(saved))
                return;

            // Format: "index:count;index:count;..."
            // Legacy format (no colon): "index;index;..." — treated as count=1 per entry.
            int index = 0;
            int usageCount = 0;
            bool parsingIndex = true;
            bool hasDigits = false;

            for (int i = 0; i <= saved.Length; i++)
            {
                char c = i < saved.Length ? saved[i] : ENTRY_SEPARATOR;

                if (c >= '0' && c <= '9')
                {
                    if (parsingIndex)
                        index = index * 10 + (c - '0');
                    else
                        usageCount = usageCount * 10 + (c - '0');

                    hasDigits = true;
                }
                else if (c == COUNT_SEPARATOR && parsingIndex && hasDigits)
                {
                    parsingIndex = false;
                    hasDigits = false;
                }
                else if (c == ENTRY_SEPARATOR && hasDigits)
                {
                    // Legacy entries without ':' get count=1.
                    if (parsingIndex)
                        usageCount = 1;

                    if (!IsFixedDefault(index) && !ContainsIndex(index))
                        usageEntries.Add(new EmojiUsage(index, usageCount));

                    index = 0;
                    usageCount = 0;
                    parsingIndex = true;
                    hasDigits = false;
                }
                else
                {
                    index = 0;
                    usageCount = 0;
                    parsingIndex = true;
                    hasDigits = false;
                }
            }
        }

        private void Save()
        {
            if (usageEntries.Count == 0)
            {
                DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, string.Empty, save: true);
                return;
            }

            saveBuilder.Clear();

            for (int i = 0; i < usageEntries.Count; i++)
            {
                if (i > 0) saveBuilder.Append(ENTRY_SEPARATOR);
                saveBuilder.Append(usageEntries[i].AtlasIndex);
                saveBuilder.Append(COUNT_SEPARATOR);
                saveBuilder.Append(usageEntries[i].Count);
            }

            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, saveBuilder.ToString(), save: true);
        }

        private bool ContainsIndex(int atlasIndex)
        {
            for (int i = 0; i < usageEntries.Count; i++)
            {
                if (usageEntries[i].AtlasIndex == atlasIndex)
                    return true;
            }

            return false;
        }

        private struct EmojiUsage
        {
            public int AtlasIndex;
            public int Count;

            public EmojiUsage(int atlasIndex, int count)
            {
                AtlasIndex = atlasIndex;
                Count = count;
            }
        }
    }
}
