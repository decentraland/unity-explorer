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

        private void Awake()
        {
            EmojiButton.onClick.AddListener(HandleButtonClick);
        }

        private void HandleButtonClick()
        {
            OnSuggestionSelected();
        }

        public override void OnGet()
        {
            gameObject.SetActive(true);
        }

        public override void OnReleased()
        {
            SelectedBackground.SetActive(false);
            gameObject.SetActive(false);
        }

        protected override void SetupContinuation(EmojiInputSuggestionData suggestionElementData)
        {
            Emoji.text = suggestionElementData.EmojiData.EmojiCode;
            EmojiName.text = suggestionElementData.EmojiData.EmojiName;
        }

        public override void SetSelectionState(bool isSelected)
        {
            base.SetSelectionState(isSelected);
            SelectedBackground.SetActive(isSelected);
        }
    }
}
