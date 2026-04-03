using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat.ChatMessages
{
    public class ReactionCountItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private RawImage emojiImage;
        [SerializeField] private TextMeshProUGUI countLabel;
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private Color defaultColor = new (0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color highlightedColor = new (0.3f, 0.2f, 0.5f, 0.8f);

        private Vector3 hoveredScale = new (1.2f, 1.2f, 1.2f);
        private float animDuration = 0.1f;
        private int emojiIndex;
        private bool hiding;

        public event Action<int>? OnClicked;
        public event Action<int, RectTransform>? OnHoverEnter;
        public event Action<int>? OnHoverExit;

        public int EmojiIndex => emojiIndex;

        public void Configure(float hoverScale, float hoverAnimDuration)
        {
            hoveredScale = new Vector3(hoverScale, hoverScale, hoverScale);
            animDuration = hoverAnimDuration;
        }

        private void Awake()
        {
            button.onClick.AddListener(() => OnClicked?.Invoke(emojiIndex));
        }

        public void SetData(int emojiIndex, int count, bool isOwnReaction, Rect uvRect, Texture atlas)
        {
            this.emojiIndex = emojiIndex;

            emojiImage.texture = atlas;
            emojiImage.uvRect = uvRect;
            countLabel.text = count.ToString();
            background.color = isOwnReaction ? highlightedColor : defaultColor;
            gameObject.SetActive(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            contentContainer.DOScale(hoveredScale, animDuration).SetEase(Ease.OutQuad);
            OnHoverEnter?.Invoke(emojiIndex, (RectTransform)transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hiding) return;

            contentContainer.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuad);
            OnHoverExit?.Invoke(emojiIndex);
        }

        public void Hide()
        {
            hiding = true;
            contentContainer.DOKill();
            contentContainer.localScale = Vector3.one;
            gameObject.SetActive(false);
            hiding = false;
        }

        private void OnDestroy()
        {
            button.onClick.RemoveAllListeners();
        }
    }
}
