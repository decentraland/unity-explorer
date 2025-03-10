using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsGoalsOfTheWeekView : MonoBehaviour
    {
        [field: SerializeField]
        public Button InfoLinkButton { get; private set; }

        [field: SerializeField]
        public Button GoShoppingButton { get; private set; }

        [field: SerializeField]
        public TMP_Text TimeLeftText { get; private set; }

        [field: SerializeField]
        public GameObject TimeLeftLoadingSpinner { get; private set; }

        [field: SerializeField]
        public TMP_Text TotalCreditsText { get; private set; }

        [field: SerializeField]
        public GameObject TotalCreditsLoadingSpinner { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingContainer { get; private set; }

        [field: SerializeField]
        public RectTransform GoalsContainer { get; private set; }

        [field: SerializeField]
        public GameObject CaptchaContainer { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsGoalRowView GoalRowPrefab { get; private set; }

        public void SetAsLoading(bool isLoading)
        {
            TimeLeftLoadingSpinner.SetActive(isLoading);
            TotalCreditsLoadingSpinner.SetActive(isLoading);
            MainLoadingContainer.SetActive(isLoading);
            TimeLeftText.gameObject.SetActive(!isLoading);
            TotalCreditsText.gameObject.SetActive(!isLoading);
            GoalsContainer.gameObject.SetActive(!isLoading);

            if (isLoading)
                CaptchaContainer.SetActive(false);
        }

        public void CleanSection()
        {
            TotalCreditsText.text = "-";
            TimeLeftText.text = "-";
        }

        public void ShowCaptcha(bool show) =>
            CaptchaContainer.SetActive(show);
    }
}
