using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    public class EmojiInputSuggestionElement : BaseInputSuggestionElement<EmojiInputSuggestionData>
    {
        [field: SerializeField] public Button EmojiButton { get; private set; }
        [field: SerializeField] public TMP_Text Emoji { get; private set; }
        [field: SerializeField] public TMP_Text EmojiName { get; private set; }
        [field: SerializeField] public GameObject SelectedBackground { get; private set; }

        private void OnEnable()
        {
            EmojiButton.onClick.AddListener(HandleButtonClick);
        }

        private void OnDisable()
        {
            EmojiButton.onClick.RemoveListener(HandleButtonClick);
        }

        private void HandleButtonClick()
        {
            OnSuggestionSelected();
        }

        protected override void SetupContinuation(EmojiInputSuggestionData suggestionElementData)
        {
            Emoji.text = suggestionElementData.EmojiData.EmojiCode;
            EmojiName.text = suggestionElementData.EmojiData.EmojiName;
        }

    }
}
