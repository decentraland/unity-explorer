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
        // Index of the top-left corner in the array RectTransform.GetWorldCorners fills
        // (bottom-left, top-left, top-right, bottom-right).
        private const int TOP_LEFT_CORNER = 1;

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

        private readonly Vector3[] anchorCorners = new Vector3[4];

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
        /// Puts the panel's bottom-left corner <paramref name="gap" /> above <paramref name="anchor" />'s top-left
        /// corner, measured through the current layout rather than from a stored position, so the result is the
        /// same whatever size the surrounding panels happen to have.
        /// </summary>
        public void PositionAbove(RectTransform anchor, float gap)
        {
            EnsureInitialized();

            anchor.GetWorldCorners(anchorCorners);

            var parent = (RectTransform)rectTransform.parent;
            Vector3 anchorTopLeft = parent.InverseTransformPoint(anchorCorners[TOP_LEFT_CORNER]);
            Rect rect = rectTransform.rect;

            // rect.xMin/yMin are the panel's edges relative to its pivot, so subtracting them turns the wanted
            // corner position into a pivot position.
            rectTransform.localPosition = new Vector3(anchorTopLeft.x - rect.xMin, anchorTopLeft.y + gap - rect.yMin, 0f);
        }
    }
}
