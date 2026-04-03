using System.Collections.Generic;
using DCL.Chat.ChatReactions.Debug;
using DCL.Prefs;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Tracks recently used emojis (excluding fixed defaults) for the shortcuts bar.
    /// Most-recently-used is first; oldest drops off when capacity is exceeded.
    /// Persists via <see cref="DCLPlayerPrefs"/> in the format <c>index;index;...</c>.
    /// </summary>
    public sealed class ChatReactionRecentsService
    {
        private const char ENTRY_SEPARATOR = ';';

        private readonly int[] fixedDefaults;
        private readonly int maxRecent;
        private readonly List<int> recentEntries = new();
        private readonly System.Text.StringBuilder saveBuilder = new();

        private bool dirty;

        /// <summary>Active instance for editor debug tools. Same pattern as <see cref="ChatReactionDebugState.Current"/>.</summary>
        public static ChatReactionRecentsService? Current { get; private set; }

        public IReadOnlyList<int> Recents => recentEntries;

        public bool IsDirty => dirty;

        internal ChatReactionRecentsService(int[] fixedDefaults, int maxRecent)
        {
            this.fixedDefaults = fixedDefaults;
            this.maxRecent = maxRecent;
            Load();
            Current = this;
        }

        /// <summary>
        /// Moves this emoji to the front of the recents list. If it already exists,
        /// it is moved to position 0. If the list exceeds capacity, the oldest (last) is removed.
        /// Fixed defaults are ignored.
        /// </summary>
        public void RecordUsage(int atlasIndex)
        {
            Profiler.BeginSample("ChatReactions.Recents.RecordUsage");

            if (IsFixedDefault(atlasIndex))
            {
                Profiler.EndSample();
                return;
            }

            // Remove if already present (will be re-inserted at front).
            for (int i = 0; i < recentEntries.Count; i++)
            {
                if (recentEntries[i] != atlasIndex) continue;
                recentEntries.RemoveAt(i);
                break;
            }

            recentEntries.Insert(0, atlasIndex);

            // Drop the oldest if over capacity.
            if (recentEntries.Count > maxRecent)
                recentEntries.RemoveAt(recentEntries.Count - 1);

            dirty = true;

            Profiler.EndSample();
        }

        /// <summary>
        /// Persists accumulated changes to disk if any were recorded
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
        /// Clears all tracked recents in memory and on disk.
        /// </summary>
        public void ClearAll()
        {
            recentEntries.Clear();
            dirty = false;
            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, string.Empty);
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

        private void Load()
        {
            Profiler.BeginSample("ChatReactions.Recents.Load");
            recentEntries.Clear();

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
        /// Zero-allocation character parser for the format <c>index;index;...</c>.
        /// Gracefully handles the legacy <c>index:count;...</c> format by ignoring the
        /// colon and everything after it within each entry.
        /// Entries beyond <see cref="maxRecent"/> are discarded.
        /// </summary>
        private void ParseSavedEntries(string saved)
        {
            int index = 0;
            bool hasDigits = false;
            bool skipUntilSeparator = false;

            for (int i = 0; i <= saved.Length; i++)
            {
                char c = i < saved.Length ? saved[i] : ENTRY_SEPARATOR;

                if (skipUntilSeparator)
                {
                    if (c == ENTRY_SEPARATOR)
                        skipUntilSeparator = false;

                    continue;
                }

                switch (c)
                {
                    case >= '0' and <= '9':
                        index = index * 10 + (c - '0');
                        hasDigits = true;
                        break;
                    case ENTRY_SEPARATOR when hasDigits:
                        if (!IsFixedDefault(index) && !recentEntries.Contains(index) && recentEntries.Count < maxRecent)
                            recentEntries.Add(index);

                        index = 0;
                        hasDigits = false;
                        break;
                    case ':' when hasDigits:
                        // Legacy format "index:count" — commit the index, skip the count.
                        if (!IsFixedDefault(index) && !recentEntries.Contains(index) && recentEntries.Count < maxRecent)
                            recentEntries.Add(index);

                        index = 0;
                        hasDigits = false;
                        skipUntilSeparator = true;
                        break;
                    default:
                        index = 0;
                        hasDigits = false;
                        break;
                }
            }
        }

        private void Save()
        {
            if (recentEntries.Count == 0)
            {
                DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, string.Empty);
                return;
            }

            saveBuilder.Clear();

            for (int i = 0; i < recentEntries.Count; i++)
            {
                if (i > 0) saveBuilder.Append(ENTRY_SEPARATOR);
                saveBuilder.Append(recentEntries[i]);
            }

            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, saveBuilder.ToString());
        }
    }
}
