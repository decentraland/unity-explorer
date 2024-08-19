using DCL.BadgesAPIService;
using DCL.Passport.Utils;
using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Passport.Fields
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

        public string BadgeCategory { get; private set; }

        public BadgeInfo Model { get; private set; }

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

        public void SetAsSelected(bool isSelected) =>
            SelectedOutline.SetActive(isSelected);

        public void Setup(BadgeInfo badgeInfo)
        {
            Model = badgeInfo;
            BadgeNameText.text = badgeInfo.name;
            BadgeCategory = badgeInfo.category;
            BadgeImage.SetColor(badgeInfo.isLocked ? LockedBadgeImageColor : NonLockedBadgeImageColor);
            BadgeDateText.text = !badgeInfo.isLocked ? PassportUtils.FormatTimestampDate(badgeInfo.awardedAt) : "--";
            BadgeDateText.gameObject.SetActive((!badgeInfo.isLocked && (!badgeInfo.isTier || (badgeInfo.isTier && badgeInfo.currentProgress == badgeInfo.totalProgress))) || badgeInfo is { isLocked: true, isTier: false });
            TopTierMark.SetActive(badgeInfo.isTier && badgeInfo.currentProgress == badgeInfo.totalProgress);
            NextTierTitle.SetActive(!badgeInfo.isLocked && badgeInfo.isTier && badgeInfo.currentProgress < badgeInfo.totalProgress);
            ProgressBar.gameObject.SetActive(badgeInfo.isTier && badgeInfo.currentProgress < badgeInfo.totalProgress);

            if (badgeInfo.isTier)
            {
                int progressPercentage = badgeInfo.isLocked ? 0 : badgeInfo.currentProgress * 100 / badgeInfo.totalProgress;
                ProgressBarFill.sizeDelta = new Vector2((!badgeInfo.isLocked ? progressPercentage : 0) * (ProgressBar.sizeDelta.x / 100), ProgressBarFill.sizeDelta.y);
            }

            imageController?.SetImage(DefaultBadgeSprite);
            if (!string.IsNullOrEmpty(badgeInfo.image))
                imageController?.RequestImage(badgeInfo.image, hideImageWhileLoading: true);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            BackgroundImage.color = HoverBackgroundColor;

        public void OnPointerExit(PointerEventData eventData) =>
            BackgroundImage.color = NormalBackgroundColor;
    }
}
