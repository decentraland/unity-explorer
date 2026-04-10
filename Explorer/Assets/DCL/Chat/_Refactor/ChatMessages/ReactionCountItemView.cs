using DG.Tweening;
using MVC;
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
        private ViewEventBus eventBus;
        private string messageId;

        public int EmojiIndex => emojiIndex;

        public void Configure(float hoverScale, float hoverAnimDuration, ViewEventBus eventBus)
        {
            hoveredScale = new Vector3(hoverScale, hoverScale, hoverScale);
            animDuration = hoverAnimDuration;
            this.eventBus = eventBus;
        }

        private void Awake()
        {
            button.onClick.AddListener(OnButtonClicked);
        }

        public void SetData(int emojiIndex, int count, bool isOwnReaction, Rect uvRect, Texture atlas, string messageId)
        {
            this.emojiIndex = emojiIndex;
            this.messageId = messageId;

            emojiImage.texture = atlas;
            emojiImage.uvRect = uvRect;
            countLabel.text = count.ToString();
            background.color = isOwnReaction ? highlightedColor : defaultColor;
            gameObject.SetActive(true);
        }

        private void OnButtonClicked()
        {
            eventBus.Publish(new ReactionPillEvents.ReactionPillClicked(messageId, emojiIndex));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            contentContainer.DOScale(hoveredScale, animDuration).SetEase(Ease.OutQuad);
            eventBus.Publish(new ReactionPillEvents.ReactionPillHoverEnter(messageId, emojiIndex, (RectTransform)transform));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hiding) return;

            contentContainer.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuad);
            eventBus.Publish(new ReactionPillEvents.ReactionPillHoverExit(emojiIndex));
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
            if (contentContainer != null)
                contentContainer.DOKill();

            button.onClick.RemoveAllListeners();
        }
    }
}
