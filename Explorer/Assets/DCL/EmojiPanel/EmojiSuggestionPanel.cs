using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Emoji
{
    public class EmojiSuggestionPanel
    {
        private readonly EmojiSuggestionPanelView view;
        private readonly IObjectPool<EmojiSuggestionView> suggestionItemsPool;
        private readonly List<EmojiSuggestionView> usedPoolItems = new ();

        public EmojiSuggestionPanel(EmojiSuggestionPanelView view, EmojiSuggestionView emojiSuggestion)
        {
            this.view = view;

            suggestionItemsPool = new ObjectPool<EmojiSuggestionView>(
                () => Object.Instantiate(emojiSuggestion, view.EmojiSuggestionContainer),
                defaultCapacity: 20,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
        }

        public void SetPanelVisibility(bool isVisible)
        {
            view.gameObject.SetActive(isVisible);
        }

        public void SetValues(IEnumerable<EmojiData> foundEmojis)
        {
            ReleaseSuggestions();
            foreach (EmojiData foundEmoji in foundEmojis)
            {
                EmojiSuggestionView emojiSuggestionView = suggestionItemsPool.Get();
                emojiSuggestionView.SetEmoji(foundEmoji);
                usedPoolItems.Add(emojiSuggestionView);
            }
        }

        private void ReleaseSuggestions()
        {
            foreach (EmojiSuggestionView emojiSuggestionView in usedPoolItems)
                suggestionItemsPool.Release(emojiSuggestionView);

            usedPoolItems.Clear();
        }
    }
}
