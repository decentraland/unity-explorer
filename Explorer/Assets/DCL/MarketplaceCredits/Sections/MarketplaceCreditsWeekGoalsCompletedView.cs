using TMPro;
using UnityEngine;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWeekGoalsCompletedView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text TimeLeftText { get; private set; }

        public void SetTimeLeftText(string timeLeft) =>
            TimeLeftText.text = timeLeft;
    }
}
