using DCL.BadgesAPIService;
using DCL.UI;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
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
        public GameObject NewMark { get; private set; }

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

        public void ConfigureImageController(IWebRequestController webRequestController, IGetTextureArgsFactory getTextureArgsFactory)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(BadgeImage, webRequestController, getTextureArgsFactory);
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

        public void SetAsNew(bool isNew) =>
            NewMark.SetActive(isNew);

        public void Setup(in BadgeInfo badgeInfo, bool isOwnProfile)
        {
            Model = badgeInfo;
            SetupBadgeName(badgeInfo);
            SetupBadgeDate(badgeInfo, isOwnProfile);
            SetupBadgeNextTier(badgeInfo, isOwnProfile);
            SetupBadgeProgressBar(badgeInfo, isOwnProfile);
            SetupBadgeImage(badgeInfo);
            SetAsNew(badgeInfo.isNew);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            BackgroundImage.color = HoverBackgroundColor;

        public void OnPointerExit(PointerEventData eventData) =>
            BackgroundImage.color = NormalBackgroundColor;

        private static bool ShouldShowBadgeProgressBar(in BadgeInfo badgeInfo, bool isOwnProfile) =>
            isOwnProfile && ((badgeInfo.data.isTier && string.IsNullOrEmpty(badgeInfo.data.completedAt)) || (!badgeInfo.data.isTier && badgeInfo.data.progress.totalStepsTarget is > 1));

        private void SetupBadgeName(in BadgeInfo badgeInfo) =>
            BadgeNameText.text = !string.IsNullOrEmpty(badgeInfo.data.progress.lastCompletedTierName) ? $"{badgeInfo.data.name} {badgeInfo.data.progress.lastCompletedTierName}" : badgeInfo.data.name;

        private void SetupBadgeDate(in BadgeInfo badgeInfo, bool isOwnProfile)
        {
            string completedAtToLoad = !string.IsNullOrEmpty(badgeInfo.data.progress.lastCompletedTierAt) ? badgeInfo.data.progress.lastCompletedTierAt : badgeInfo.data.completedAt;
            BadgeDateText.text = !string.IsNullOrEmpty(completedAtToLoad) ? BadgesUtils.FormatTimestampDate(completedAtToLoad) : "—";

            BadgeDateText.gameObject.SetActive(
                !ShouldShowBadgeProgressBar(badgeInfo, isOwnProfile) &&
                ((!badgeInfo.isLocked && !string.IsNullOrEmpty(badgeInfo.data.completedAt)) ||
                 (badgeInfo.isLocked && !badgeInfo.data.isTier) ||
                 (!isOwnProfile && !string.IsNullOrEmpty(badgeInfo.data.progress.lastCompletedTierAt))));
        }

        private void SetupBadgeNextTier(in BadgeInfo badgeInfo, bool isOwnProfile)
        {
            TopTierMark.SetActive(isOwnProfile && badgeInfo.data.isTier && !string.IsNullOrEmpty(badgeInfo.data.completedAt));
            NextTierTitle.SetActive(isOwnProfile && badgeInfo.data.isTier && badgeInfo.data.progress.stepsDone > 0 && string.IsNullOrEmpty(badgeInfo.data.completedAt));
        }

        private void SetupBadgeProgressBar(in BadgeInfo badgeInfo, bool isOwnProfile)
        {
            bool showProgressBar = ShouldShowBadgeProgressBar(badgeInfo, isOwnProfile);
            ProgressBar.gameObject.SetActive(showProgressBar);

            if (!isOwnProfile)
                return;

            if (badgeInfo.data.isTier)
            {
                int progressPercentage = badgeInfo.GetProgressPercentage();
                ProgressBarFill.sizeDelta = new Vector2(progressPercentage * (ProgressBar.sizeDelta.x / 100), ProgressBarFill.sizeDelta.y);
            }
            else
            {
                int simpleBadgeProgressPercentage = badgeInfo.data.progress.stepsDone * 100 / badgeInfo.data.progress.totalStepsTarget;
                ProgressBarFill.sizeDelta = new Vector2(simpleBadgeProgressPercentage * (ProgressBar.sizeDelta.x / 100), ProgressBarFill.sizeDelta.y);
            }
        }

        private void SetupBadgeImage(in BadgeInfo badgeInfo)
        {
            BadgeImage.SetColor(badgeInfo.isLocked ? LockedBadgeImageColor : NonLockedBadgeImageColor);
            imageController?.SetImage(DefaultBadgeSprite);

            string imageToLoad = !string.IsNullOrEmpty(badgeInfo.data.progress.lastCompletedTierImage) ?
                badgeInfo.data.progress.lastCompletedTierImage :
                badgeInfo.data.assets is { textures2d: not null } ? badgeInfo.data.assets.textures2d.normal : "";

            if (!string.IsNullOrEmpty(imageToLoad))
                imageController?.RequestImage(imageToLoad, hideImageWhileLoading: true);
        }
    }
}
