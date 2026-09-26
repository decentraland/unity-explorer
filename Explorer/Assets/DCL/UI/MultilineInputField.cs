using TMPro;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    /// <summary>
    ///     A <see cref="TMP_InputField" /> whose Enter key keeps inserting new lines in MultiLineNewline mode:
    ///     the stock <see cref="TMP_InputField.OnSubmit" /> deactivates the field even in that mode.
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
