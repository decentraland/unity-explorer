using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Emoji
{
    public class EmojiSuggestionPanel
    {
        public event Action<EmojiData> OnEmojiSelected;

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

        public void SetPanelVisibility(bool isVisible) =>
            view.gameObject.SetActive(isVisible);

        public void SetValues(List<EmojiData> foundEmojis)
        {
            for(int i = foundEmojis.Count; i < usedPoolItems.Count; i++)
                suggestionItemsPool.Release(usedPoolItems[i]);

            for(int i = usedPoolItems.Count - 1; i >= foundEmojis.Count; i--)
                usedPoolItems.RemoveAt(i);

            for(int i = 0; i < foundEmojis.Count; i++)
            {
                EmojiData foundEmoji = foundEmojis[i];
                if(usedPoolItems.Count > i)
                {
                    usedPoolItems[i].SetEmoji(foundEmoji);
                    usedPoolItems[i].EmojiButton.onClick.RemoveAllListeners();
                    usedPoolItems[i].EmojiButton.onClick.AddListener(() => OnEmojiSelected?.Invoke(foundEmoji));
                }
                else
                {
                    EmojiSuggestionView emojiSuggestionView = suggestionItemsPool.Get();
                    emojiSuggestionView.SetEmoji(foundEmoji);
                    emojiSuggestionView.EmojiButton.onClick.RemoveAllListeners();
                    emojiSuggestionView.EmojiButton.onClick.AddListener(() => OnEmojiSelected?.Invoke(foundEmoji));
                    usedPoolItems.Add(emojiSuggestionView);
                }
            }
        }
    }
}
