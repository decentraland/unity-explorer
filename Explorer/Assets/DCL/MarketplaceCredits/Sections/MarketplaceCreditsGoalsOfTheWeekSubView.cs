using DCL.MarketplaceCredits.Fields;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsGoalsOfTheWeekSubView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text TimeLeftText { get; private set; }

        [field: SerializeField]
        public Button TimeLeftInfoButton { get; private set; }

        [field: SerializeField]
        public GameObject TimeLeftLinkTooltip { get; private set; }

        [field: SerializeField]
        public RectTransform GoalsContainer { get; private set; }

        [field: SerializeField]
        public GameObject CaptchaContainer { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsCaptchaView CaptchaControl { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsGoalRowView GoalRowPrefab { get; private set; }

        public void SetTimeLeftText(string text) =>
            TimeLeftText.text = text;

        public void ToggleTimeLeftTooltip() =>
            TimeLeftLinkTooltip.SetActive(!TimeLeftLinkTooltip.activeSelf);

        public void ShowTimeLeftTooltip(bool show) =>
            TimeLeftLinkTooltip.SetActive(show);

        public void ShowCaptcha(bool show) =>
            CaptchaContainer.SetActive(show);

        public void SetCaptchaAsLoading(bool isLoading) =>
            CaptchaControl.SetAsLoading(isLoading);

        public void SetCaptchaPercentageValue(float value) =>
            CaptchaControl.SetCaptchaPercentageValue(value);

        public void SetCaptchaTargetAreaImage(Sprite sprite) =>
            CaptchaControl.SetTargetAreaImage(sprite);

        public void SetCaptchaAsErrorState(bool isError, bool isNonSolvedError = true) =>
            CaptchaControl.SetAsErrorState(isError, isNonSolvedError);
    }
}
