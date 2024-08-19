using DCL.BadgesAPIService;
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

        public void Setup(BadgeInfo badgeInfo)
        {
            BadgeNameText.text = badgeInfo.name;
            BadgeDateText.text = !badgeInfo.isLocked ? $"Unlocked: {PassportUtils.FormatTimestampDate(badgeInfo.awardedAt)}" : "Locked";
            BadgeDescriptionText.text = badgeInfo.description;
            Badge2DImage.SetColor(badgeInfo.isLocked ? LockedImageColor : UnlockedImageColor);

            imageController?.SetImage(DefaultBadgeSprite);
            if (!string.IsNullOrEmpty(badgeInfo.image))
                imageController?.RequestImage(badgeInfo.image, hideImageWhileLoading: true);
        }

        public void SetAsLoading(bool isLoading)
        {
            MainLoadingSpinner.SetActive(isLoading);
            MainContainer.SetActive(!isLoading);
        }
    }
}
