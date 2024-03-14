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
        public event Action<EmojiData> OnEmojiSelected;
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
                () => Object.Instantiate(emojiButton, parent),
                defaultCapacity: 20,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
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


        public void SetValues(IEnumerable<EmojiData> foundEmojis)
        {
            ReleaseSearchResults();
            foreach (EmojiData foundEmoji in foundEmojis)
            {
                EmojiButton emojiView = searchItemsPool.Get();
                emojiView.EmojiImage.text = foundEmoji.EmojiCode;
                emojiView.TooltipText.text = foundEmoji.EmojiName;
                emojiView.Button.onClick.AddListener(() => OnEmojiSelected?.Invoke(foundEmoji));
                usedPoolItems.Add(emojiView);
            }
        }

        private void ReleaseSearchResults()
        {
            foreach (EmojiButton emojiSuggestionView in usedPoolItems)
                searchItemsPool.Release(emojiSuggestionView);

            usedPoolItems.Clear();
        }
    }
}
