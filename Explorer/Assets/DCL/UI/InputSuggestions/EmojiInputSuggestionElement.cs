using TMPro;
using UnityEngine;

namespace DCL.UI.SuggestionPanel
{
    public class EmojiInputSuggestionElement : BaseInputSuggestionElement<EmojiInputSuggestionData>
    {
        [field: SerializeField] private TMP_Text emoji;
        [field: SerializeField] private TMP_Text emojiName;

        protected override void SetupContinuation(EmojiInputSuggestionData suggestionElementData)
        {
            emoji.text = suggestionElementData.EmojiData.EmojiCode;
            emojiName.text = suggestionElementData.EmojiData.EmojiName;
        }
    }
}
