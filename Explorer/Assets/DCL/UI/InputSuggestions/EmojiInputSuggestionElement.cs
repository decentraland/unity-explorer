using TMPro;
using UnityEngine;

namespace DCL.UI.SuggestionPanel
{
    public class EmojiInputSuggestionElement : BaseInputSuggestionElement<EmojiInputSuggestionData>
    {
        [field: SerializeField] private TMP_Text emoji;
        [field: SerializeField] private TMP_Text emojiName;

        protected override void SetupContinuation(EmojiInputSuggestionData inputSuggestionElementData)
        {
            SuggestionId = inputSuggestionElementData.EmojiCode;
            emoji.text = inputSuggestionElementData.EmojiCode;
            emojiName.text = inputSuggestionElementData.EmojiName;
        }
    }
}
