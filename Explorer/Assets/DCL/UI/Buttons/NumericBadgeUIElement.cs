using TMPro;
using UnityEngine;

namespace DCL.UI.Buttons
{
    /// <summary>
    /// The typical behaviour of a numeric badge that appears over an icon or button.
    /// </summary>
    public class NumericBadgeUIElement : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text numberText;

        [SerializeField]
        private GameObject numberPanel;

        [Tooltip("If the number is greater than the maximum, a + sign will be displayed next to the maximum value.")]
        [SerializeField]
        private int maximum = 1;

        private int currentValue;
        private bool isMentionsVisible;

        public int Number
        {
            get => currentValue;

            set
            {
                if (currentValue != value && !isMentionsVisible)
                {
                    currentValue = value;
                    numberText.text = currentValue > maximum ? $"+{maximum}" : currentValue.ToString();
                }

                numberPanel.SetActive(currentValue > 0);
            }
        }

        /// <summary>
        /// It replaces the number with an '@'. Afterwards, changing the number will not show the number until the mention sign is hidden.
        /// </summary>
        /// <param name="show">When True, it replaces the number; otherwise it deactivates the mention sign (setting the number will show the number).</param>
        public void ShowMentionsSign(bool show)
        {
            isMentionsVisible = show;

            if (isMentionsVisible)
            {
                numberText.text = "@";
                numberPanel.SetActive(true);
            }
        }
    }
}
