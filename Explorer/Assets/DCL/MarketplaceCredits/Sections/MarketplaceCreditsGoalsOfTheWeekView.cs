using DCL.MarketplaceCredits.Fields;
using TMPro;
using UnityEngine;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsGoalsOfTheWeekView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text TimeLeftText { get; private set; }

        [field: SerializeField]
        public GameObject TimeLeftLoadingSpinner { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingContainer { get; private set; }

        [field: SerializeField]
        public RectTransform GoalsContainer { get; private set; }

        [field: SerializeField]
        public GameObject CaptchaContainer { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsCaptchaView CaptchaControl { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsGoalRowView GoalRowPrefab { get; private set; }

        public void SetAsLoading(bool isLoading)
        {
            TimeLeftLoadingSpinner.SetActive(isLoading);
            MainLoadingContainer.SetActive(isLoading);
            TimeLeftText.gameObject.SetActive(!isLoading);
            GoalsContainer.gameObject.SetActive(!isLoading);

            if (isLoading)
                CaptchaContainer.SetActive(false);
        }

        public void ShowCaptcha(bool show) =>
            CaptchaContainer.SetActive(show);

        public void SetCaptchaAsLoading(bool isLoading) =>
            CaptchaControl.SetAsLoading(isLoading);

        public void SetCaptchaPercentageValue(float value) =>
            CaptchaControl.SetCaptchaPercentageValue(value);

        public void SetCaptchaTargetAreaPercentageValue(float value) =>
            CaptchaControl.SetTargetAreaPercentageValue(value);

        public void SetCaptchaAsErrorState(bool isError) =>
            CaptchaControl.SetAsErrorState(isError);
    }
}
