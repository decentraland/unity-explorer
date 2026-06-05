using System;
using System.Collections.Generic;
using DCL.UI;
using SuperScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiPanelView : MonoBehaviour
    {
        public event Action<int, bool>? SectionSelected;
        public event Action<string>? SearchTextChanged;
        public event Action? SearchInputFocused;
        public event Action? SearchInputBlurred;

        [field: SerializeField]
        public List<EmojiSectionToggle> EmojiSections { get; private set; }

        [field: SerializeField]
        public LoopListView2 EmojiLoopList { get; private set; }

        [field: SerializeField]
        public SearchBarView SearchPanelView { get; private set; }

        [field: SerializeField]
        public EmojiTooltipView TooltipView { get; private set; }

        [field: SerializeField]
        public RectMask2D ViewportMask { get; private set; }

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private bool isInitialized;

        /// <summary>
        /// The panel's default anchored position captured before any consumer moves it.
        /// Uses anchoredPosition (not world position) so it stays correct across resolutions.
        /// </summary>
        public Vector2 DefaultAnchoredPosition { get; private set; }

        public bool IsVisible => isInitialized && canvasGroup.alpha > 0f;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            for (int i = 0; i < EmojiSections.Count; i++)
            {
                EmojiSections[i].Index = i;
                EmojiSections[i].SectionSelected += OnSectionToggleSelected;
            }

            TMP_InputField inputField = SearchPanelView.inputField;
            inputField.onValueChanged.AddListener(HandleSearchInputChanged);
            inputField.onSelect.AddListener(HandleSearchInputSelected);
            inputField.onDeselect.AddListener(HandleSearchInputDeselected);
            SearchPanelView.clearSearchButton.onClick.AddListener(ClearSearchText);
            SearchPanelView.clearSearchButton.gameObject.SetActive(false);
        }

        private void OnSectionToggleSelected(int sectionIndex, bool isOn) =>
            SectionSelected?.Invoke(sectionIndex, isOn);

        private void HandleSearchInputSelected(string _) =>
            SearchInputFocused?.Invoke();

        private void HandleSearchInputDeselected(string _) =>
            SearchInputBlurred?.Invoke();

        public void ClearSearchText() =>
            SearchPanelView.inputField.text = string.Empty;

        public void FocusSearchInput()
        {
            SearchPanelView.inputField.Select();
            SearchPanelView.inputField.ActivateInputField();
        }

        public void BlurSearchInput()
        {
            if (SearchPanelView.inputField.isFocused)
                SearchPanelView.inputField.DeactivateInputField();
        }

        private void HandleSearchInputChanged(string text)
        {
            SearchPanelView.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
            SearchTextChanged?.Invoke(text);
        }

        private void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            rectTransform = (RectTransform)transform;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            DefaultAnchoredPosition = rectTransform.anchoredPosition;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

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
            ViewportMask.enabled = visible;
        }

        /// <summary>
        /// Resets the panel to its default anchored position (the chat-input position).
        /// </summary>
        public void ResetToDefaultPosition()
        {
            EnsureInitialized();
            rectTransform.anchoredPosition = DefaultAnchoredPosition;
        }

        /// <summary>
        /// Moves the panel to the given world position.
        /// Callers should pre-apply any desired offset before calling.
        /// </summary>
        public void MoveTo(Vector3 worldPosition)
        {
            EnsureInitialized();
            rectTransform.position = worldPosition;
        }
    }
}
