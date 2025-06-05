using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Fields
{
    public class MarketplaceCreditsGoalRowView : MonoBehaviour
    {
        [field: SerializeField]
        public Image MainBackgroundImage { get; private set; }

        [field: SerializeField]
        public ImageView GoalImage { get; private set; }

        [field: SerializeField]
        public Sprite DefaultGoalSprite { get; private set; }

        [field: SerializeField]
        public TMP_Text GoalTitleText { get; private set; }

        [field: SerializeField]
        public RectTransform ProgressBar { get; private set; }

        [field: SerializeField]
        public RectTransform ProgressBarFill { get; private set; }

        [field: SerializeField]
        public TMP_Text ProgressValueText { get; private set; }

        [field: SerializeField]
        public GameObject CompletedMark { get; private set; }

        [field: SerializeField]
        public GameObject ClaimedMark { get; private set; }

        [field: SerializeField]
        public GameObject PendingToClaimMark { get; private set; }

        [field: SerializeField]
        public GameObject PendingToClaimOutline { get; private set; }

        [field: SerializeField]
        public TMP_Text GoalCreditsValueText { get; private set; }

        [field: SerializeField]
        public CanvasGroup CreditsCanvasGroup { get; private set; }

        [field: SerializeField]
        public float AlphaValueForClaimedCredits { get; private set; }

        private ImageController imageController;

        public void ConfigureImageController(ISpriteCache spriteCache)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(GoalImage, spriteCache);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void SetupGoalImage(string imageUrl)
        {
            imageController?.SetImage(DefaultGoalSprite);

            if (!string.IsNullOrEmpty(imageUrl))
                imageController?.RequestImage(imageUrl, hideImageWhileLoading: true);
        }

        public void SetTitle(string goalTitle) =>
            GoalTitleText.text = goalTitle;

        public void SetCredits(string credits) =>
            GoalCreditsValueText.text = credits;

        public void SetAsCompleted(bool isCompleted, bool isClaimed)
        {
            CompletedMark.SetActive(isCompleted && !isClaimed);
            ClaimedMark.SetActive(isClaimed);
            ProgressBar.gameObject.SetActive(!isCompleted);
        }

        public void SetClaimStatus(bool isPendingToClaim, bool isClaimed)
        {
            MainBackgroundImage.enabled = !isPendingToClaim;
            PendingToClaimMark.SetActive(isPendingToClaim);
            PendingToClaimOutline.SetActive(isPendingToClaim);
            CreditsCanvasGroup.alpha = isClaimed ? AlphaValueForClaimedCredits : 1f;
        }

        public void SetProgress(uint progressPercentage, uint stepsDone, uint totalSteps)
        {
            ProgressBarFill.sizeDelta = new Vector2(Mathf.Clamp(progressPercentage, 0, 100) * (ProgressBar.sizeDelta.x / 100), ProgressBarFill.sizeDelta.y);
            ProgressValueText.text = $"{stepsDone}/{totalSteps}";
        }
    }
}
