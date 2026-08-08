using TMPro;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    /// <summary>
    ///     A <see cref="TMP_InputField" /> whose Enter key keeps inserting new lines while in
    ///     MultiLineNewline mode. The EventSystem fires the UI Submit action at the focused field,
    ///     and the stock <see cref="TMP_InputField.OnSubmit" /> deactivates it even in that mode,
    ///     right after the key event added the line break. In every other line type it behaves
    ///     exactly like the base field.
    /// </summary>
    public class MultilineInputField : TMP_InputField
    {
        public override void OnSubmit(BaseEventData eventData)
        {
            if (lineType == LineType.MultiLineNewline)
                return;

            base.OnSubmit(eventData);
        }
    }
}
