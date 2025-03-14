using DCL.MarketplaceCredits.Fields;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsGoalsOfTheWeekView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject TimeLeftContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text TimeLeftText { get; private set; }

        [field: SerializeField]
        public GameObject TimeLeftLoadingSpinner { get; private set; }

        [field: SerializeField]
        public Button TimeLeftLinkButton { get; private set; }

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
            MainLoadingContainer.SetActive(isLoading);
            TimeLeftLoadingSpinner.SetActive(isLoading);
            TimeLeftContainer.gameObject.SetActive(!isLoading);
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

        public void SetCaptchaAsErrorState(bool isError, bool isNonSolvedError = true) =>
            CaptchaControl.SetAsErrorState(isError, isNonSolvedError);
    }
}
