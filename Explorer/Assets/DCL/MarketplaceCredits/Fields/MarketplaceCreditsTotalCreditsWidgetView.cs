using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Fields
{
    public class MarketplaceCreditsTotalCreditsWidgetView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject TotalCreditsContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text TotalCreditsText { get; private set; }

        [field: SerializeField]
        public TMP_Text DaysToExpireText { get; private set; }

        [field: SerializeField]
        public Button GoShoppingButton { get; private set; }

        public void SetCredits(string creditsText) =>
            TotalCreditsText.text = creditsText;

        public void SetDaysToExpire(string daysToExpireText) =>
            DaysToExpireText.text = daysToExpireText;

        public void SetDaysToExpireVisible(bool isVisible) =>
            DaysToExpireText.gameObject.SetActive(isVisible);
    }
}
