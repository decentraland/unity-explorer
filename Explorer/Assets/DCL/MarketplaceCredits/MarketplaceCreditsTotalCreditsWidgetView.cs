using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsTotalCreditsWidgetView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text TotalCreditsText { get; private set; }

        [field: SerializeField]
        public Button GoShoppingButton { get; private set; }

        [field: SerializeField]
        public GameObject TotalCreditsLoadingSpinner { get; private set; }

        public void SetAsLoading(bool isLoading)
        {
            TotalCreditsLoadingSpinner.SetActive(isLoading);
            TotalCreditsText.gameObject.SetActive(!isLoading);
        }

        public void SetCredits(string credits) =>
            TotalCreditsText.text = credits;
    }
}
