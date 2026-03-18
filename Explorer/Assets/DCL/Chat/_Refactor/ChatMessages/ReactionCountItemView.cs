using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatMessages
{
    public class ReactionCountItemView : MonoBehaviour
    {
        [SerializeField] private RawImage emojiImage;
        [SerializeField] private TextMeshProUGUI countLabel;
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private Color defaultColor = new (0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color highlightedColor = new (0.3f, 0.2f, 0.5f, 0.8f);

        private int emojiIndex;

        public event Action<int>? OnClicked;

        public int EmojiIndex => emojiIndex;

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
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

        public void Hide()
        {
            gameObject.SetActive(false);
            OnClicked = null;
        }

        private void OnDestroy()
        {
            button.onClick.RemoveAllListeners();
        }
    }
}
