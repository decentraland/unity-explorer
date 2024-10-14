using DCL.BadgesAPIService;
using DCL.UI;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Passport.Fields.Badges
{
    public class BadgeOverviewItem_PassportFieldView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public ImageView BadgeImage { get; private set; }

        [field: SerializeField]
        public Sprite DefaultBadgeSprite { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeNameText { get; private set; }

        [field: SerializeField]
        private GameObject badgeNameTooltip;

        private ImageController? imageController;

        private void OnEnable() =>
            SetBadgeNameToastActive(false);

        public void ConfigureImageController(IWebRequestController webRequestController, IGetTextureArgsFactory getTextureArgsFactory)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(BadgeImage, webRequestController, getTextureArgsFactory);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void Setup(LatestAchievedBadgeData badgeData)
        {
            SetupBadgeName(badgeData);
            SetupBadgeImage(badgeData);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            SetBadgeNameToastActive(true);

        public void OnPointerExit(PointerEventData eventData) =>
            SetBadgeNameToastActive(false);

        private void SetupBadgeName(LatestAchievedBadgeData badgeData) =>
            BadgeNameText.text = $"{badgeData.name} {(string.IsNullOrEmpty(badgeData.tierName) ? string.Empty : badgeData.tierName)}";

        private void SetupBadgeImage(LatestAchievedBadgeData badgeData)
        {
            imageController?.SetImage(DefaultBadgeSprite);
            if (!string.IsNullOrEmpty(badgeData.image))
                imageController?.RequestImage(badgeData.image, hideImageWhileLoading: true);
        }

        private void SetBadgeNameToastActive(bool isActive) =>
            badgeNameTooltip.SetActive(isActive);
    }
}
