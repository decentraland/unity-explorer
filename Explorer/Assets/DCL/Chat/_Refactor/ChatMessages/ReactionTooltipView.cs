using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatMessages
{
    public class ReactionTooltipView : MonoBehaviour
    {
        private const string OFFLINE_MESSAGE = "Offline user — reactions unavailable.";

        [SerializeField] private TMP_Text namesText;
        [SerializeField] private RawImage emojiImage;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform arrowTransform;
        [Tooltip("Required because tooltip is parented under a zero-size RectTransform (ChatBody). " +
                 "Points to a reference element whose center defines horizontal centering.")]
        [SerializeField] private RectTransform centeringReference;

        private ReactionTooltipPositioner? positioner;

        public RectTransform? ArrowTransform => arrowTransform;
        public RectTransform? CenteringReference => centeringReference;

        public void Initialize(ReactionTooltipPositioner positioner)
        {
            this.positioner = positioner;
        }

        public void Show(string text, Rect emojiUvRect, Texture atlas, RectTransform pillTransform)
        {
            ShowContent(text, emojiUvRect, atlas);
            positioner?.PositionAbovePill(pillTransform);
            SetVisible(true);
        }

        public void ShowLoading(Rect emojiUvRect, Texture atlas, RectTransform pillTransform)
        {
            emojiImage.texture = atlas;
            emojiImage.uvRect = emojiUvRect;
            ShowLoadingContent();
            positioner?.PositionAbovePill(pillTransform);
            SetVisible(true);
        }

        public void UpdateText(string text)
        {
            loadingIndicator.SetActive(false);
            namesText.gameObject.SetActive(true);
            emojiImage.gameObject.SetActive(true);
            namesText.text = text;
        }

        public void ShowOfflineMessage(RectTransform pillTransform)
        {
            loadingIndicator.SetActive(false);
            emojiImage.gameObject.SetActive(false);
            namesText.gameObject.SetActive(true);
            namesText.text = OFFLINE_MESSAGE;
            positioner?.PositionAbovePill(pillTransform);
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void ShowContent(string text, Rect emojiUvRect, Texture atlas)
        {
            loadingIndicator.SetActive(false);
            namesText.gameObject.SetActive(true);
            emojiImage.gameObject.SetActive(true);

            namesText.text = text;
            emojiImage.texture = atlas;
            emojiImage.uvRect = emojiUvRect;
        }

        private void ShowLoadingContent()
        {
            loadingIndicator.SetActive(true);
            namesText.gameObject.SetActive(false);
            emojiImage.gameObject.SetActive(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            gameObject.SetActive(visible);
        }
    }
}
