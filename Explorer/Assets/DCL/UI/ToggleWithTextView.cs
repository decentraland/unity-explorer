using TMPro;
using UnityEngine;

namespace DCL.UI
{
    public class ToggleWithTextView : ToggleView
    {
        [field: SerializeField]
        public TMP_Text toggleText { get; private set; }

        public void SetToggleText(string text)
        {
            toggleText.text = text;
        }
    }
}
