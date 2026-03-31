using System.Collections.Generic;
using DCL.Prefs;
using UnityEngine.Profiling;

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

        private bool dirty;

        /// <summary>Active instance for editor debug tools. Same pattern as <see cref="ChatReactionDebugState.Current"/>.</summary>
        public static ChatReactionRecentsService? Current { get; private set; }

        public IReadOnlyList<int> Recents => topRecents;

        public bool IsDirty => dirty;

        public int TotalTrackedEmojis => usageEntries.Count;

        public ChatReactionRecentsService(int[] fixedDefaults, int maxRecent)
        {
            this.fixedDefaults = fixedDefaults;
            this.maxRecent = maxRecent;
            Load();
            RebuildTopRecents();
            Current = this;
        }

        /// <summary>
        /// Increments the usage count for this emoji. Fixed defaults are ignored.
        /// Call this every time the user sends a non-default emoji.
        /// </summary>
        public void RecordUsage(int atlasIndex)
        {
            Profiler.BeginSample("ChatReactions.Recents.RecordUsage");

            if (IsFixedDefault(atlasIndex))
            {
                Profiler.EndSample();
                return;
            }

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

            Profiler.BeginSample("ChatReactions.Recents.RebuildTop");
            RebuildTopRecents();
            Profiler.EndSample();

            dirty = true;

            Profiler.EndSample();
        }

        /// <summary>
        /// Persists accumulated usage changes to disk if any were recorded
        /// since the last flush. Safe to call multiple times — no-op when clean.
        /// </summary>
        public void FlushIfDirty()
        {
            if (!dirty) return;

            Profiler.BeginSample("ChatReactions.Recents.Flush");
            Save();
            dirty = false;
            Profiler.EndSample();
        }

        /// <summary>
        /// Clears all tracked usage data in memory and on disk.
        /// </summary>
        public void ClearAll()
        {
            usageEntries.Clear();
            topRecents.Clear();
            dirty = false;
            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, string.Empty);
        }

        /// <summary>
        /// Returns atlas index and usage count for the entry at the given position.
        /// For editor debug display only.
        /// </summary>
        public (int atlasIndex, int count) GetUsageEntry(int i) =>
            (usageEntries[i].AtlasIndex, usageEntries[i].Count);

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
            Profiler.BeginSample("ChatReactions.Recents.Load");
            usageEntries.Clear();

            string? saved = ReadSavedFavorites();

            if (saved != null)
                ParseSavedEntries(saved);

            Profiler.EndSample();
        }
        private static string? ReadSavedFavorites()
        {
            if (!DCLPlayerPrefs.HasKey(DCLPrefKeys.CHAT_REACTION_FAVORITES))
                return null;

            string saved = DCLPlayerPrefs.GetString(DCLPrefKeys.CHAT_REACTION_FAVORITES);
            return string.IsNullOrEmpty(saved) ? null : saved;
        }

        /// <summary>
        /// Zero-allocation character parser for the format <c>index:count;index:count;...</c>.
        /// Legacy format (no colon): <c>index;index;...</c> — treated as count=1 per entry.
        /// </summary>
        private void ParseSavedEntries(string saved)
        {
            int index = 0;
            int usageCount = 0;
            bool parsingIndex = true;
            bool hasDigits = false;

            for (int i = 0; i <= saved.Length; i++)
            {
                char c = i < saved.Length ? saved[i] : ENTRY_SEPARATOR;

                switch (c)
                {
                    case >= '0' and <= '9':
                        {
                            if (parsingIndex)
                                index = index * 10 + (c - '0');
                            else
                                usageCount = usageCount * 10 + (c - '0');

                            hasDigits = true;
                            break;
                        }
                    case COUNT_SEPARATOR when parsingIndex && hasDigits:
                        parsingIndex = false;
                        hasDigits = false;
                        break;
                    case ENTRY_SEPARATOR when hasDigits:
                        CommitParsedEntry(ref index, ref usageCount, ref parsingIndex, ref hasDigits);
                        break;
                    default:
                        ResetParseState(ref index, ref usageCount, ref parsingIndex, ref hasDigits);
                        break;
                }
            }
        }

        private void CommitParsedEntry(ref int index, ref int usageCount, ref bool parsingIndex, ref bool hasDigits)
        {
            if (parsingIndex)
                usageCount = 1;

            if (!IsFixedDefault(index) && !ContainsIndex(index))
                usageEntries.Add(new EmojiUsage(index, usageCount));

            ResetParseState(ref index, ref usageCount, ref parsingIndex, ref hasDigits);
        }

        private static void ResetParseState(ref int index, ref int usageCount, ref bool parsingIndex, ref bool hasDigits)
        {
            index = 0;
            usageCount = 0;
            parsingIndex = true;
            hasDigits = false;
        }

        private void Save()
        {
            if (usageEntries.Count == 0)
            {
                DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, string.Empty);
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

            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, saveBuilder.ToString());
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
            public readonly int AtlasIndex;
            public int Count;

            public EmojiUsage(int atlasIndex, int count)
            {
                AtlasIndex = atlasIndex;
                Count = count;
            }
        }
    }
}
