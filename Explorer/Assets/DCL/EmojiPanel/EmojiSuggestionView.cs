using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiSuggestionView : MonoBehaviour
    {
        public event Action<string> OnEmojiSelected;

        [field: SerializeField]
        public Button EmojiButton { get; private set; }

        [field: SerializeField]
        public TMP_Text Emoji { get; private set; }

        [field: SerializeField]
        public TMP_Text EmojiName { get; private set; }

        [field: SerializeField]
        public GameObject SelectedBackground { get; private set; }

        private void Start()
        {
            EmojiButton.onClick.RemoveAllListeners();
            EmojiButton.onClick.AddListener(HandleButtonClick);
        }

        private void HandleButtonClick()
        {
            OnEmojiSelected?.Invoke(Emoji.text);
        }

        public void SetEmoji(EmojiData emojiData)
        {
            Emoji.text = emojiData.EmojiCode;
            EmojiName.text = emojiData.EmojiName;
        }
    }
}
