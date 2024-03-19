using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Emoji
{
    public class EmojiSearchController
    {
        public event Action<string> OnEmojiSelected;
        public event Action<string> OnSearchTextChanged;

        private readonly SearchBarView view;
        private CancellationTokenSource cts;
        private readonly IObjectPool<EmojiButton> searchItemsPool;
        private readonly List<EmojiButton> usedPoolItems = new ();

        public EmojiSearchController(SearchBarView view, Transform parent, EmojiButton emojiButton)
        {
            this.view = view;

            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);

            searchItemsPool = new ObjectPool<EmojiButton>(
                () => CreatePoolElements(parent, emojiButton),
                defaultCapacity: 20,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
        }

        private EmojiButton CreatePoolElements(Transform parent, EmojiButton emojiButton)
        {
            EmojiButton poolElement = Object.Instantiate(emojiButton, parent);
            poolElement.OnEmojiSelected += (emojiCode) => OnEmojiSelected?.Invoke(emojiCode);
            return poolElement;
        }

        private void ClearSearch()
        {
            view.inputField.text = string.Empty;
            view.clearSearchButton.gameObject.SetActive(false);
        }

        private void OnValueChanged(string searchText)
        {
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            OnSearchTextChanged?.Invoke(searchText);
        }

        public void SetValues(List<EmojiData> foundEmojis)
        {
            for(int i = foundEmojis.Count; i < usedPoolItems.Count; i++)
                searchItemsPool.Release(usedPoolItems[i]);

            for(int i = usedPoolItems.Count - 1; i >= foundEmojis.Count; i--)
                usedPoolItems.RemoveAt(i);

            for(int i = 0; i < foundEmojis.Count; i++)
            {
                EmojiData foundEmoji = foundEmojis[i];
                if(usedPoolItems.Count > i)
                {
                    usedPoolItems[i].EmojiImage.text = foundEmoji.EmojiCode;
                    usedPoolItems[i].TooltipText.text = foundEmoji.EmojiName;
                }
                else
                {
                    EmojiButton emojiView = searchItemsPool.Get();
                    emojiView.EmojiImage.text = foundEmoji.EmojiCode;
                    emojiView.TooltipText.text = foundEmoji.EmojiName;
                    usedPoolItems.Add(emojiView);
                }
            }
        }

        private void ReleaseAllSearchResults()
        {
            foreach (EmojiButton emojiSuggestionView in usedPoolItems)
                searchItemsPool.Release(emojiSuggestionView);

            usedPoolItems.Clear();
        }

        public void Dispose()
        {
            ReleaseAllSearchResults();
        }
    }
}
