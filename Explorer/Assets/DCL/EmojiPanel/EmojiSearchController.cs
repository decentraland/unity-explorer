using DCL.Input;
using DCL.Input.Component;
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
        public event Action<string> EmojiSelected;
        public event Action<string> SearchTextChanged;

        private readonly SearchBarView view;
        private CancellationTokenSource cts;
        private readonly IObjectPool<EmojiButton> searchItemsPool;
        private readonly List<EmojiButton> usedPoolItems = new ();
        private readonly IInputBlock? inputBlock;
        private bool shortcutsBlocked;

        public EmojiSearchController(SearchBarView view, Transform parent, EmojiButton emojiButton, IInputBlock? inputBlock = null)
        {
            this.view = view;
            this.inputBlock = inputBlock;

            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.inputField.onSelect.AddListener(BlockShortcuts);
            view.inputField.onDeselect.AddListener(RestoreShortcuts);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);

            searchItemsPool = new ObjectPool<EmojiButton>(
                () => CreatePoolElements(parent, emojiButton),
                defaultCapacity: 20,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );
        }

        public void Dispose()
        {
            ReleaseAllSearchResults();
            RestoreShortcuts(string.Empty);
            view.inputField.onValueChanged.RemoveListener(OnValueChanged);
            view.inputField.onSelect.RemoveListener(BlockShortcuts);
            view.inputField.onDeselect.RemoveListener(RestoreShortcuts);
            view.clearSearchButton.onClick.RemoveListener(ClearSearch);
        }

        private void BlockShortcuts(string _)
        {
            if (inputBlock == null || shortcutsBlocked) return;
            shortcutsBlocked = true;
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);
        }

        private void RestoreShortcuts(string _)
        {
            if (inputBlock == null || !shortcutsBlocked) return;
            shortcutsBlocked = false;
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);
        }

        private EmojiButton CreatePoolElements(Transform parent, EmojiButton emojiButton)
        {
            EmojiButton poolElement = Object.Instantiate(emojiButton, parent);
            poolElement.EmojiSelected += (emojiCode) => EmojiSelected?.Invoke(emojiCode);
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
            SearchTextChanged?.Invoke(searchText);
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
    }
}
