using DCL.BadgesAPIService;
using DCL.Passport.Utils;
using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Passport.Fields.Badges
{
    public class BadgeDetailCard_PassportFieldView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public RectTransform SubContainerTransform { get; private set; }

        [field: SerializeField]
        public Image BackgroundImage { get; private set; }

        [field: SerializeField]
        public Button Button { get; private set; }

        [field: SerializeField]
        public ImageView BadgeImage { get; private set; }

        [field: SerializeField]
        public Sprite DefaultBadgeSprite { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeNameText { get; private set; }

        [field: SerializeField]
        public GameObject SelectedOutline { get; private set; }

        [field: SerializeField]
        public GameObject TopTierMark { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeDateText { get; private set; }

        [field: SerializeField]
        public GameObject NextTierTitle { get; private set; }

        [field: SerializeField]
        public RectTransform ProgressBar { get; private set; }

        [field: SerializeField]
        public RectTransform ProgressBarFill { get; private set; }

        [field: SerializeField]
        public Color NormalBackgroundColor { get; private set; }

        [field: SerializeField]
        public Color HoverBackgroundColor { get; private set; }

        [field: SerializeField]
        public Color LockedBadgeImageColor { get; private set; }

        [field: SerializeField]
        public Color NonLockedBadgeImageColor { get; private set; }

        public BadgeInfo Model { get; private set; }

        public bool IsSelected { get; private set; }

        private ImageController? imageController;

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(BadgeImage, webRequestController);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void SetInvisible(bool isInvisible) =>
            SubContainerTransform.gameObject.SetActive(!isInvisible);

        public void SetAsSelected(bool isSelected)
        {
            SelectedOutline.SetActive(isSelected);
            IsSelected = isSelected;
        }

        public void Setup(BadgeInfo badgeInfo, bool isOwnProfile)
        {
            Model = badgeInfo;
            bool showProgressBar = isOwnProfile && ((badgeInfo.isTier && string.IsNullOrEmpty(badgeInfo.completedAt)) || (!badgeInfo.isTier && badgeInfo.progress.totalStepsTarget is > 1));
            BadgeNameText.text = !string.IsNullOrEmpty(badgeInfo.progress.lastCompletedTierName) ? $"{badgeInfo.name} {badgeInfo.progress.lastCompletedTierName}" : badgeInfo.name;
            BadgeImage.SetColor(badgeInfo.isLocked ? LockedBadgeImageColor : NonLockedBadgeImageColor);
            string completedAtToLoad = !string.IsNullOrEmpty(badgeInfo.progress.lastCompletedTierAt) ? badgeInfo.progress.lastCompletedTierAt : badgeInfo.completedAt;
            BadgeDateText.text = !string.IsNullOrEmpty(completedAtToLoad) ? BadgesUtils.FormatTimestampDate(completedAtToLoad) : "—";
            BadgeDateText.gameObject.SetActive(
                !showProgressBar &&
                ((!badgeInfo.isLocked && !string.IsNullOrEmpty(badgeInfo.completedAt)) ||
                 badgeInfo is { isLocked: true, isTier: false } ||
                 (!isOwnProfile && !string.IsNullOrEmpty(badgeInfo.progress.lastCompletedTierAt))));
            TopTierMark.SetActive(isOwnProfile && badgeInfo.isTier && !string.IsNullOrEmpty(badgeInfo.completedAt));
            NextTierTitle.SetActive(isOwnProfile && badgeInfo.isTier && badgeInfo.progress.stepsDone > 0 && string.IsNullOrEmpty(badgeInfo.completedAt));
            ProgressBar.gameObject.SetActive(showProgressBar);

            if (isOwnProfile)
            {
                if (badgeInfo.isTier)
                {
                    int progressPercentage = badgeInfo.isLocked ? 0 : badgeInfo.progress.stepsDone * 100 / (badgeInfo.progress.nextStepsTarget ?? badgeInfo.progress.totalStepsTarget);
                    ProgressBarFill.sizeDelta = new Vector2((!badgeInfo.isLocked ? progressPercentage : 0) * (ProgressBar.sizeDelta.x / 100), ProgressBarFill.sizeDelta.y);
                }
                else
                {
                    int simpleBadgeProgressPercentage = badgeInfo.progress.stepsDone * 100 / badgeInfo.progress.totalStepsTarget;
                    ProgressBarFill.sizeDelta = new Vector2(simpleBadgeProgressPercentage * (ProgressBar.sizeDelta.x / 100), ProgressBarFill.sizeDelta.y);
                }
            }

            imageController?.SetImage(DefaultBadgeSprite);
            string imageToLoad = !string.IsNullOrEmpty(badgeInfo.progress.lastCompletedTierImage) ?
                badgeInfo.progress.lastCompletedTierImage :
                badgeInfo.assets != null && badgeInfo.assets.textures2d != null ? badgeInfo.assets.textures2d.normal : "";

            if (!string.IsNullOrEmpty(imageToLoad))
                imageController?.RequestImage(imageToLoad, hideImageWhileLoading: true);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            BackgroundImage.color = HoverBackgroundColor;

        public void OnPointerExit(PointerEventData eventData) =>
            BackgroundImage.color = NormalBackgroundColor;
    }
}
