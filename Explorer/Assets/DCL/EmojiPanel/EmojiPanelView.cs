using System;
using System.Collections.Generic;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiPanelView : MonoBehaviour
    {
        public event Action<float, bool> SectionSelected;
        public event Action EmojiFirstOpen;

        [field: SerializeField]
        public List<EmojiSectionToggle> EmojiSections { get; private set; }

        [field: SerializeField]
        public ScrollRect ScrollView { get; private set; }

        [field: SerializeField]
        public Transform EmojiContainer { get; private set; }

        [field: SerializeField]
        public Transform EmojiContainerScrollView { get; private set; }

        [field: SerializeField]
        public Transform EmojiSearchResults { get; private set; }

        [field: SerializeField]
        public Transform EmojiSearchedContent { get; private set; }

        [field: SerializeField]
        public SearchBarView SearchPanelView { get; private set; }

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private bool initialized;

        /// <summary>
        /// The panel's default anchored position captured before any consumer moves it.
        /// Uses anchoredPosition (not world position) so it stays correct across resolutions.
        /// </summary>
        public Vector2 DefaultAnchoredPosition { get; private set; }

        public bool IsVisible => initialized && canvasGroup.alpha > 0f;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EmojiFirstOpen?.Invoke();

            foreach (EmojiSectionToggle emojiSectionToggle in EmojiSections)
                emojiSectionToggle.SectionToggle.onValueChanged.AddListener((isOn) => SectionSelected?.Invoke(emojiSectionToggle.SectionPosition, isOn));
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            rectTransform = (RectTransform)transform;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            DefaultAnchoredPosition = rectTransform.anchoredPosition;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (!EmojiContainer.gameObject.activeSelf)
                EmojiContainer.gameObject.SetActive(true);

            SetVisible(false);
        }

        /// <summary>
        /// Shows or hides the panel using CanvasGroup (no SetActive overhead).
        /// The GameObject stays active so subsequent shows are instant.
        /// </summary>
        public void SetVisible(bool visible)
        {
            EnsureInitialized();

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        /// <summary>
        /// Resets the panel to its default anchored position (the chat-input position).
        /// </summary>
        public void ResetToDefaultPosition()
        {
            EnsureInitialized();
            rectTransform.anchoredPosition = DefaultAnchoredPosition;
        }
    }
}
