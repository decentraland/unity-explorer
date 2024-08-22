using DCL.BadgesAPIService;
using DCL.Passport.Fields.Badges;
using DCL.Passport.Utils;
using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules.Badges
{
    public class BadgeInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject MainContainer { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; }

        [field: SerializeField]
        public ImageView LockedBadge2DImage { get; private set; }

        [field: SerializeField]
        public RawImage UnlockedBadge3DImage { get; private set; }

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
        public GameObject NextTierProgressBarContainer { get; private set; }

        [field: SerializeField]
        public RectTransform NextTierProgressBarFill { get; private set; }

        [field: SerializeField]
        public TMP_Text NextTierProgressValueText { get; private set; }

        private ImageController? imageController;

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(LockedBadge2DImage, webRequestController);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void Setup(BadgeInfo badgeInfo, bool isOwnProfile)
        {
            TierSection.SetActive(badgeInfo.isTier);
            LockedBadge2DImage.gameObject.SetActive(badgeInfo.isLocked);
            UnlockedBadge3DImage.gameObject.SetActive(!badgeInfo.isLocked);
            imageController?.SetImage(DefaultBadgeSprite);

            if (!badgeInfo.isTier)
            {
                BadgeNameText.text = badgeInfo.name;
                BadgeDateText.text = !badgeInfo.isLocked ? $"Unlocked: {PassportUtils.FormatTimestampDate(badgeInfo.completedAt)}" : "Locked";
                BadgeDescriptionText.text = badgeInfo.description;

                if (!string.IsNullOrEmpty(badgeInfo.image) && badgeInfo.isLocked)
                    imageController?.RequestImage(badgeInfo.image, hideImageWhileLoading: true);
            }
            else
            {
                var nextTierToComplete = badgeInfo.tiers[badgeInfo.nextTierToCompleteIndex];
                TopTierMark.SetActive(isOwnProfile && !string.IsNullOrEmpty(badgeInfo.completedAt));
                NextTierContainer.SetActive(isOwnProfile && string.IsNullOrEmpty(badgeInfo.completedAt) && badgeInfo.nextTierCurrentProgress > 0);
                NextTierValueText.text = nextTierToComplete.name;
                NextTierDescriptionText.text = nextTierToComplete.description;
                NextTierDescriptionText.gameObject.SetActive(isOwnProfile);
                int nextTierProgressPercentage = badgeInfo.isLocked ? 0 : badgeInfo.nextTierCurrentProgress * 100 / badgeInfo.nextTierTotalProgress;
                NextTierProgressBarFill.sizeDelta = new Vector2((!badgeInfo.isLocked ? nextTierProgressPercentage : 0) * (NextTierProgressBar.sizeDelta.x / 100), NextTierProgressBarFill.sizeDelta.y);
                NextTierProgressValueText.text = $"{badgeInfo.nextTierCurrentProgress}/{badgeInfo.nextTierTotalProgress}";
                NextTierProgressBarContainer.SetActive(isOwnProfile);
            }
        }

        public void SelectBadgeTier(int tierIndex, BadgeInfo badgeInfo)
        {
            var tier = badgeInfo.tiers[tierIndex];
            BadgeNameText.text = $"{badgeInfo.name} {tier.name}";
            BadgeDateText.text = !tier.isLocked ? $"Unlocked: {PassportUtils.FormatTimestampDate(tier.awardedAt)}" : "Locked";
            BadgeDescriptionText.text = tier.description;
        }

        public void SetAsLoading(bool isLoading)
        {
            MainLoadingSpinner.SetActive(isLoading);
            MainContainer.SetActive(!isLoading);
        }
    }
}
