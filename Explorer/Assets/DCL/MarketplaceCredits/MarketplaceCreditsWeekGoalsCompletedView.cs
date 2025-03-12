using TMPro;
using UnityEngine;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsWeekGoalsCompletedView : MonoBehaviour
    {
        [field: SerializeField]
        public MarketplaceCreditsTotalCreditsWidgetView TotalCreditsWidget { get; private set; }

        [field: SerializeField]
        public TMP_Text TimeLeftText { get; private set; }

        public void SetAsLoading(bool isLoading)
        {
            TotalCreditsWidget.SetAsLoading(isLoading);
        }
    }
}
