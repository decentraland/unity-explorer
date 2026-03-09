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
        private readonly System.Text.StringBuilder saveBuilder = new();

        public IReadOnlyList<int> Favorites => favorites;

        public int SelectedIndex { get; private set; } = -1;

        public ChatReactionFavoritesService(int[] defaultIndices)
        {
            this.defaultIndices = defaultIndices;
            Load();
            LoadSelected();
        }

        public bool TryGetFirstFavorite(out int atlasIndex)
        {
            if (favorites.Count > 0)
            {
                atlasIndex = favorites[0];
                return true;
            }

            atlasIndex = -1;
            return false;
        }

        public void SetSelected(int atlasIndex)
        {
            if (SelectedIndex == atlasIndex) return;
            SelectedIndex = atlasIndex;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.CHAT_REACTION_SELECTED, atlasIndex, save: true);
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

            int value = 0;
            bool parsing = false;

            for (int i = 0; i <= saved.Length; i++)
            {
                char c = i < saved.Length ? saved[i] : SEPARATOR;

                if (c >= '0' && c <= '9')
                {
                    value = value * 10 + (c - '0');
                    parsing = true;
                }
                else if (c == SEPARATOR && parsing)
                {
                    if (!favorites.Contains(value))
                        favorites.Add(value);

                    value = 0;
                    parsing = false;
                }
                else
                {
                    value = 0;
                    parsing = false;
                }
            }
        }

        private void LoadSelected()
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.CHAT_REACTION_SELECTED))
                SelectedIndex = DCLPlayerPrefs.GetInt(DCLPrefKeys.CHAT_REACTION_SELECTED);
            else if (TryGetFirstFavorite(out int first))
                SelectedIndex = first;
        }

        private void Save()
        {
            if (favorites.Count == 0)
            {
                DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, string.Empty, save: true);
                return;
            }

            saveBuilder.Clear();

            for (int i = 0; i < favorites.Count; i++)
            {
                if (i > 0) saveBuilder.Append(SEPARATOR);
                saveBuilder.Append(favorites[i]);
            }

            DCLPlayerPrefs.SetString(DCLPrefKeys.CHAT_REACTION_FAVORITES, saveBuilder.ToString(), save: true);
        }
    }
}
