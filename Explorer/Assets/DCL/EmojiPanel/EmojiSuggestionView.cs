using TMPro;
using UnityEngine;

namespace DCL.Emoji
{
    public class EmojiSuggestionView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Emoji { get; private set; }

        [field: SerializeField]
        public TMP_Text EmojiName { get; private set; }

        public void SetEmoji(EmojiData emojiData)
        {
            Emoji.text = emojiData.EmojiCode;
            EmojiName.text = emojiData.EmojiName;
        }
    }
}
