using DCL.BadgesAPIService;
using DCL.Passport.Fields.Badges;
using DCL.Passport.Utils;
using DCL.UI;
using DCL.WebRequests;
using System.Collections.Generic;
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
        private List<TierData> currentTiers = new ();

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(LockedBadge2DImage, webRequestController);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void Setup(BadgeInfo badgeInfo, List<TierData> tiers, bool isOwnProfile)
        {
            currentTiers = tiers;
            TierSection.SetActive(badgeInfo.isTier);
            LockedBadge2DImage.gameObject.SetActive(badgeInfo.isLocked);
            UnlockedBadge3DImage.gameObject.SetActive(!badgeInfo.isLocked);
            imageController?.SetImage(DefaultBadgeSprite);

            if (!badgeInfo.isTier)
            {
                BadgeNameText.text = badgeInfo.name;
                BadgeDateText.text = !badgeInfo.isLocked ? $"Unlocked: {BadgesUtils.FormatTimestampDate(badgeInfo.completedAt)}" : "Locked";
                BadgeDescriptionText.text = badgeInfo.description;

                if (!string.IsNullOrEmpty(badgeInfo.image) && badgeInfo.isLocked)
                    imageController?.RequestImage(badgeInfo.image, hideImageWhileLoading: true);
            }
            else
            {
                var nextTierToCompleteIndex = 0;
                for (var i = 0; i < tiers.Count; i++)
                {
                    if (badgeInfo.progress.nextStepsTarget == tiers[i].criteria.steps)
                        nextTierToCompleteIndex = i;
                }

                var nextTierToComplete = tiers[nextTierToCompleteIndex];
                TopTierMark.SetActive(isOwnProfile && !string.IsNullOrEmpty(badgeInfo.completedAt));
                NextTierContainer.SetActive(isOwnProfile && string.IsNullOrEmpty(badgeInfo.completedAt) && badgeInfo.progress.stepsDone > 0);
                NextTierDescriptionText.gameObject.SetActive(isOwnProfile);
                NextTierProgressBarContainer.SetActive(isOwnProfile);

                if (isOwnProfile)
                {
                    NextTierValueText.text = nextTierToComplete.tierName;
                    NextTierDescriptionText.text = nextTierToComplete.description;
                    int nextTierProgressPercentage = badgeInfo.isLocked ? 0 : badgeInfo.progress.stepsDone!.Value * 100 / (badgeInfo.progress.nextStepsTarget ?? badgeInfo.progress.totalStepsTarget!.Value);
                    NextTierProgressBarFill.sizeDelta = new Vector2((!badgeInfo.isLocked ? nextTierProgressPercentage : 0) * (NextTierProgressBar.sizeDelta.x / 100), NextTierProgressBarFill.sizeDelta.y);
                    NextTierProgressValueText.text = $"{badgeInfo.progress.stepsDone}/{badgeInfo.progress.nextStepsTarget ?? badgeInfo.progress.totalStepsTarget}";
                }
            }
        }

        public void SelectBadgeTier(int tierIndex, BadgeInfo badgeInfo)
        {
            var tier = currentTiers[tierIndex];
            BadgeNameText.text = $"{badgeInfo.name} {tier.tierName}";
            string tierCompletedAt = badgeInfo.GetTierCompletedDate(tier.tierId);
            BadgeDateText.text = !string.IsNullOrEmpty(tierCompletedAt) ? $"Unlocked: {BadgesUtils.FormatTimestampDate(tierCompletedAt)}" : "Locked";
            BadgeDescriptionText.text = tier.description;
        }

        public void SetAsLoading(bool isLoading)
        {
            MainLoadingSpinner.SetActive(isLoading);
            MainContainer.SetActive(!isLoading);
        }
    }
}
