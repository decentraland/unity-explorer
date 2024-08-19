using DCL.BadgesAPIService;
using DCL.Passport.Fields;
using DCL.Passport.Utils;
using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;

namespace DCL.Passport.Modules
{
    public class BadgeInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject MainContainer { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; }

        [field: SerializeField]
        public ImageView Badge2DImage { get; private set; }

        [field: SerializeField]
        public Sprite DefaultBadgeSprite { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeNameText { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeDateText { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeDescriptionText { get; private set; }

        [field: SerializeField]
        public GameObject TierSection { get; private set; }

        [field: SerializeField]
        public BadgeTierButton_PassportFieldView BadgeTierButtonPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform AllTiersContainer { get; private set; }

        [field: SerializeField]
        public GameObject TopTierMark { get; private set; }

        [field: SerializeField]
        public GameObject NextTierContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text NextTierValueText { get; private set; }

        [field: SerializeField]
        public TMP_Text NextTierDescriptionText { get; private set; }

        [field: SerializeField]
        public RectTransform NextTierProgressBar { get; private set; }

        [field: SerializeField]
        public RectTransform NextTierProgressBarFill { get; private set; }

        [field: SerializeField]
        public TMP_Text NextTierProgressValueText { get; private set; }

        [field: SerializeField]
        public Color UnlockedImageColor { get; private set; }

        [field: SerializeField]
        public Color LockedImageColor { get; private set; }

        private ImageController? imageController;

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(Badge2DImage, webRequestController);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void Setup(BadgeInfo badgeInfo, bool isOwnProfile)
        {
            TierSection.SetActive(badgeInfo.tiers.Length > 0);
            Badge2DImage.SetColor(badgeInfo.isLocked ? LockedImageColor : UnlockedImageColor);
            imageController?.SetImage(DefaultBadgeSprite);
            if (!string.IsNullOrEmpty(badgeInfo.image))
                imageController?.RequestImage(badgeInfo.image, hideImageWhileLoading: true);

            if (badgeInfo.tiers.Length == 0)
            {
                BadgeNameText.text = badgeInfo.name;
                BadgeDateText.text = !badgeInfo.isLocked ? $"Unlocked: {PassportUtils.FormatTimestampDate(badgeInfo.awardedAt)}" : "Locked";
                BadgeDescriptionText.text = badgeInfo.description;
            }
            else
                SelectTier(0, badgeInfo);
        }

        public void SelectTier(int tierIndex, BadgeInfo badgeInfo)
        {
            var tier = badgeInfo.tiers[tierIndex];
            BadgeTierInfo? nextTier = badgeInfo.currentTier < badgeInfo.tiers.Length - 1 ? badgeInfo.tiers[badgeInfo.currentTier + 1] : null;
            BadgeNameText.text = tier.name;
            BadgeDateText.text = !tier.isLocked ? $"Unlocked: {PassportUtils.FormatTimestampDate(tier.awardedAt)}" : "Locked";
            BadgeDescriptionText.text = tier.description;
            TopTierMark.SetActive(badgeInfo.currentProgress == badgeInfo.totalProgress);
            NextTierContainer.SetActive(badgeInfo.currentProgress < badgeInfo.totalProgress);
            NextTierValueText.text = nextTier != null ? nextTier.name : tier.name;
            NextTierDescriptionText.text = nextTier != null ? nextTier.description : badgeInfo.description;
            int tierProgressPercentage = tier.isLocked ? 0 : badgeInfo.currentProgress * 100 / badgeInfo.totalProgress;
            NextTierProgressBarFill.sizeDelta = new Vector2((!tier.isLocked ? tierProgressPercentage : 0) * (NextTierProgressBar.sizeDelta.x / 100), NextTierProgressBarFill.sizeDelta.y);
            NextTierProgressValueText.text = $"{badgeInfo.currentProgress}/{badgeInfo.totalProgress}";
        }

        public void SetAsLoading(bool isLoading)
        {
            MainLoadingSpinner.SetActive(isLoading);
            MainContainer.SetActive(!isLoading);
        }
    }
}
