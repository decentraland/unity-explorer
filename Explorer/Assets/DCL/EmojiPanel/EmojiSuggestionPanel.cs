using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Emoji
{
    public class EmojiSuggestionPanel : IViewWithGlobalDependencies
    {
        public event Action<string, bool> EmojiSelected;

        public bool IsActive { get; private set; }

        private readonly EmojiSuggestionPanelView view;
        private readonly IObjectPool<EmojiSuggestionView> suggestionItemsPool;
        private readonly List<EmojiSuggestionView> usedPoolItems = new ();

        private readonly float minHeight = 50;
        private readonly float entryHeight = 34;
        private readonly float padding = 16;
        private readonly float maxHeight = 340;
        private int currentIndex = 0;
        private EmojiSuggestionView previouslySelected;

        private ViewDependencies viewDependencies;

        public EmojiSuggestionPanel(EmojiSuggestionPanelView view, EmojiSuggestionView emojiSuggestion)
        {
            this.view = view;

            suggestionItemsPool = new ObjectPool<EmojiSuggestionView>(
                () => CreatePoolElement(view, emojiSuggestion),
                defaultCapacity: 20,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView =>
                {
                    buttonView.gameObject.SetActive(false);
                    buttonView.SelectedBackground.SetActive(false);
                }
            );
        }

        private void OnSubmit(InputAction.CallbackContext obj)
        {
            if (previouslySelected != null && IsActive)
                EmojiSelected?.Invoke(previouslySelected.Emoji.text, false);
        }

        private void OnArrowUp(InputAction.CallbackContext obj)
        {
            if (currentIndex > 0)
                SetSelectedEmoji(currentIndex - 1);
            else
                SetSelectedEmoji(usedPoolItems.Count - 1);
        }

        private void OnArrowDown(InputAction.CallbackContext obj)
        {
            if (currentIndex < usedPoolItems.Count - 1)
                SetSelectedEmoji(currentIndex + 1);
            else
                SetSelectedEmoji(0);
        }

        private EmojiSuggestionView CreatePoolElement(EmojiSuggestionPanelView view, EmojiSuggestionView emojiSuggestion)
        {
            EmojiSuggestionView emojiSuggestionView = Object.Instantiate(emojiSuggestion, view.EmojiSuggestionContainer);
            emojiSuggestionView.OnEmojiSelected += (emojiData) => EmojiSelected?.Invoke(emojiData, true);
            return emojiSuggestionView;
        }

        public void SetValues(List<EmojiData> foundEmojis)
        {
            view.NoResults.gameObject.SetActive(foundEmojis.Count == 0);
            view.ScrollViewComponent.vertical = foundEmojis.Count > 7;

            switch (foundEmojis.Count)
            {
                case <= 1:
                    view.ScrollView.sizeDelta = new Vector2(view.ScrollView.sizeDelta.x, minHeight);
                    break;
                case <= 7:
                    view.ScrollView.sizeDelta = new Vector2(view.ScrollView.sizeDelta.x, (entryHeight * foundEmojis.Count) + padding);
                    break;
                default:
                    view.ScrollView.sizeDelta = new Vector2(view.ScrollView.sizeDelta.x, maxHeight);
                    break;
            }

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
                    usedPoolItems[i].gameObject.transform.SetAsLastSibling();
                }
                else
                {
                    EmojiSuggestionView emojiSuggestionView = suggestionItemsPool.Get();
                    emojiSuggestionView.SetEmoji(foundEmoji);
                    emojiSuggestionView.gameObject.transform.SetAsLastSibling();
                    usedPoolItems.Add(emojiSuggestionView);
                }
            }

            if(usedPoolItems.Count > 0)
                SetSelectedEmoji(0);
            else
                previouslySelected = null;
        }

        private void SetSelectedEmoji(int index)
        {
            if (!view.gameObject.activeInHierarchy)
                return;

            if (previouslySelected != null)
                previouslySelected.SelectedBackground.SetActive(false);

            currentIndex = index;
            usedPoolItems[index].SelectedBackground.SetActive(true);
            previouslySelected = usedPoolItems[index];
        }

        public void SetDrawOrder(CanvasOrdering order)
        {
            // Empt
        }

        public UniTask ShowAsync(CancellationToken ct)
        {
            viewDependencies.DclInput.Player.ActionForward.performed += OnArrowUp;
            viewDependencies.DclInput.Player.ActionBackward.performed += OnArrowDown;
            viewDependencies.DclInput.UI.Submit.performed += OnSubmit;
            IsActive = true;
            view.gameObject.SetActive(true);
            return UniTask.CompletedTask;
        }

        public UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            viewDependencies.DclInput.Player.ActionForward.performed -= OnArrowUp;
            viewDependencies.DclInput.Player.ActionBackward.performed -= OnArrowDown;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmit;
            IsActive = false;
            view.gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }

        public void SetCanvasActive(bool isActive)
        {
            // Empty
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }
    }
}
