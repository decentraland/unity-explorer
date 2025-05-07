using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class CharacterCounterView : MonoBehaviour
    {

        [field: SerializeField]
        internal Color lowCountColor { get; private set; }

        [field: SerializeField]
        internal Color halfCountColor { get; private set; }

        [field: SerializeField]
        internal Color fullCountColor { get; private set; }

        [field: SerializeField]
        internal Image fillerImage { get; private set; }

        private int maxCharacterCount = 100;

        public void SetMaximumLength(int maxCount)
        {
            maxCharacterCount = maxCount;
        }

        public void SetCharacterCount(int currentCount)
        {
            float fillAmount = (float)currentCount / maxCharacterCount;
            fillerImage.fillAmount = fillAmount;

            fillerImage.color = fillAmount < 0.5f ? lowCountColor : fillAmount < 0.8f ? halfCountColor : fullCountColor;
        }
    }
}
