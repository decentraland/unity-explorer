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

        public int Number
        {
            get => currentValue;

            set
            {
                if (currentValue != value)
                {
                    currentValue = value;
                    numberText.text = currentValue > maximum ? $"+{maximum}" : currentValue.ToString();
                    numberPanel.SetActive(currentValue > 0);
                }
            }
        }
    }
}
