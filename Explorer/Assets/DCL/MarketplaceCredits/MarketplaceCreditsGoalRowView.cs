using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsGoalRowView : MonoBehaviour
    {
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
        public GameObject PendingToClaimMark { get; private set; }

        [field: SerializeField]
        public GameObject PendingToClaimOutline { get; private set; }

        [field: SerializeField]
        public TMP_Text GoalCreditsValueText { get; private set; }

        private ImageController imageController;

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(GoalImage, webRequestController);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void SetupGoalImage(string imageUrl)
        {
            imageController?.SetImage(DefaultGoalSprite);

            if (!string.IsNullOrEmpty(imageUrl))
                imageController?.RequestImage(imageUrl, hideImageWhileLoading: true);
        }

        public void SetAsCompleted(bool isCompleted)
        {
            CompletedMark.SetActive(isCompleted);
            ProgressBar.gameObject.SetActive(!isCompleted);
        }

        public void SetAsPendingToClaim(bool isPendingToClaim)
        {
            PendingToClaimMark.SetActive(isPendingToClaim);
            PendingToClaimOutline.SetActive(isPendingToClaim);
        }

        public void SetProgressBarPercentage(int progressPercentage) =>
            ProgressBarFill.sizeDelta = new Vector2(Mathf.Clamp(progressPercentage, 0, 100) * (ProgressBar.sizeDelta.x / 100), ProgressBarFill.sizeDelta.y);
    }
}
