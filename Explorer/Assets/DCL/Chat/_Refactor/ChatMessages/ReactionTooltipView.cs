using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatMessages
{
    public class ReactionTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text namesText;
        [SerializeField] private RawImage emojiImage;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Vector2 offset = new (0f, 12f);

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        public void Show(string text, Rect emojiUvRect, Texture atlas, Vector3 worldPosition)
        {
            loadingIndicator.SetActive(false);
            namesText.gameObject.SetActive(true);
            emojiImage.gameObject.SetActive(true);

            namesText.text = text;
            emojiImage.texture = atlas;
            emojiImage.uvRect = emojiUvRect;

            PositionAbove(worldPosition);
            SetVisible(true);
        }

        public void ShowLoading(Vector3 worldPosition)
        {
            loadingIndicator.SetActive(true);
            namesText.gameObject.SetActive(false);
            emojiImage.gameObject.SetActive(false);

            PositionAbove(worldPosition);
            SetVisible(true);
        }

        public void UpdateText(string text)
        {
            loadingIndicator.SetActive(false);
            namesText.gameObject.SetActive(true);
            emojiImage.gameObject.SetActive(true);
            namesText.text = text;
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void PositionAbove(Vector3 worldPosition)
        {
            rectTransform.position = new Vector3(
                worldPosition.x + offset.x,
                worldPosition.y + offset.y,
                worldPosition.z);
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
