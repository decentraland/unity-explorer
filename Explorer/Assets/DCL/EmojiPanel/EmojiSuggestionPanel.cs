using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Emoji
{
    public class EmojiSuggestionPanel
    {
        public event Action<string> OnEmojiSelected;

        private readonly EmojiSuggestionPanelView view;
        private readonly IObjectPool<EmojiSuggestionView> suggestionItemsPool;
        private readonly List<EmojiSuggestionView> usedPoolItems = new ();

        public EmojiSuggestionPanel(EmojiSuggestionPanelView view, EmojiSuggestionView emojiSuggestion)
        {
            this.view = view;

            suggestionItemsPool = new ObjectPool<EmojiSuggestionView>(
                () => CreatePoolElement(view, emojiSuggestion),
                defaultCapacity: 20,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
        }

        private EmojiSuggestionView CreatePoolElement(EmojiSuggestionPanelView view, EmojiSuggestionView emojiSuggestion)
        {
            EmojiSuggestionView emojiSuggestionView = Object.Instantiate(emojiSuggestion, view.EmojiSuggestionContainer);
            emojiSuggestionView.OnEmojiSelected += (emojiData) => OnEmojiSelected?.Invoke(emojiData);
            return emojiSuggestionView;
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
                }
                else
                {
                    EmojiSuggestionView emojiSuggestionView = suggestionItemsPool.Get();
                    emojiSuggestionView.SetEmoji(foundEmoji);
                    usedPoolItems.Add(emojiSuggestionView);
                }
            }
        }
    }
}
