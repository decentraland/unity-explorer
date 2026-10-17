using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Emoji
{
    public class EmojiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<string>? EmojiSelected;
        public event Action<EmojiButton>? EmojiHovered;
        public event Action<EmojiButton>? EmojiUnhovered;

        [field: SerializeField]
        public TMP_Text EmojiImage { get; private set; }

        [field: SerializeField]
        public Button Button { get; private set; }

        public string EmojiCode { get; private set; } = string.Empty;
        public string EmojiName { get; private set; } = string.Empty;
        public RectTransform RectTransform => (RectTransform)transform;

        private bool isHovered;

        public void SetValues(string emojiCode, string emojiName)
        {
            ClearHoverState();

            EmojiCode = emojiCode;
            EmojiName = emojiName;
            EmojiImage.text = EmojiCode;
            gameObject.SetActive(true);
        }

        public void SetCallbacks(
            Action<string> emojiSelected,
            Action<EmojiButton> emojiHovered,
            Action<EmojiButton> emojiUnhovered)
        {
            EmojiSelected = emojiSelected;
            EmojiHovered = emojiHovered;
            EmojiUnhovered = emojiUnhovered;
        }

        public void SetEmpty()
        {
            ClearHoverState();
            EmojiSelected = null;
            EmojiHovered = null;
            EmojiUnhovered = null;
            EmojiCode = string.Empty;
            EmojiName = string.Empty;
            EmojiImage.text = string.Empty;
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(EmojiName))
                return;

            isHovered = true;
            EmojiHovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearHoverState();
        }

        private void Start() =>
            Button.onClick.AddListener(HandleButtonClick);

        private void OnDisable()
        {
            ClearHoverState();
        }

        private void HandleButtonClick()
        {
            EmojiSelected?.Invoke(EmojiCode);
            gameObject.DeselectIfSelected();
        }

        private void ClearHoverState()
        {
            if (!isHovered)
                return;

            isHovered = false;
            EmojiUnhovered?.Invoke(this);
        }
    }
}
