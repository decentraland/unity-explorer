using TMPro;
using UnityEngine;

namespace DCL.UI
{
    public class ToggleWithTextView : ToggleView
    {
        [field: SerializeField]
        public TMP_Text toggleText { get; private set; }
        [field: SerializeField]
        private Color interactableTextColor { get; set; }
        [field: SerializeField]
        private Color nonInteractableTextColor { get; set; }

        public void SetToggleText(string text)
        {
            toggleText.text = text;
        }

        public void SetInteractable(bool isInteractable)
        {
            toggleText.color = isInteractable ? interactableTextColor : nonInteractableTextColor;
            Toggle.interactable = isInteractable;
        }
    }
}
