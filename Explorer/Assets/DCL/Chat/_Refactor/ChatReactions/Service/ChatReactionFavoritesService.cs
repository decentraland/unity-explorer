using System.Collections.Generic;
using DCL.Prefs;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Persists the user's favorite emoji atlas indices via <see cref="DCLPlayerPrefs"/>.
    /// Falls back to config-provided defaults on first launch.
    /// </summary>
    public sealed class ChatReactionFavoritesService
    {
        private const char SEPARATOR = ';';

        private readonly int[] defaultIndices;
        private readonly List<int> favorites = new();

        public IReadOnlyList<int> Favorites => favorites;

        public ChatReactionFavoritesService(int[] defaultIndices)
        {
            this.defaultIndices = defaultIndices;
            Load();
        }

        public void Add(int atlasIndex)
        {
            if (favorites.Contains(atlasIndex))
                return;

            favorites.Add(atlasIndex);
            Save();
        }

        public void Remove(int atlasIndex)
        {
            if (!favorites.Remove(atlasIndex))
                return;

            Save();
        }

        private void Load()
        {
            favorites.Clear();

            if (!DCLPlayerPrefs.HasKey(DCLPrefKeys.CHAT_REACTION_FAVORITES))
            {
                for (int i = 0; i < defaultIndices.Length; i++)
                    favorites.Add(defaultIndices[i]);

                return;
            }

            string saved = DCLPlayerPrefs.GetString(DCLPrefKeys.CHAT_REACTION_FAVORITES);

            if (string.IsNullOrEmpty(saved))
                return;

            string[] parts = saved.Split(SEPARATOR);

            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int index))
                    favorites.Add(index);
            }
        }

        private void Save()
        {
            if (favorites.Count == 0)
            {
                DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, string.Empty, save: true);
                return;
            }

            // Manual join to avoid LINQ / string.Join with IEnumerable
            var sb = new System.Text.StringBuilder(favorites.Count * 4);

            for (int i = 0; i < favorites.Count; i++)
            {
                if (i > 0) sb.Append(SEPARATOR);
                sb.Append(favorites[i]);
            }

            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, sb.ToString(), save: true);
        }
    }
}
