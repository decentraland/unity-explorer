using DCL.BadgesAPIService;
using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Passport.Fields
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

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(BadgeImage, webRequestController);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void Setup(BadgeInfo badgeInfo)
        {
            BadgeNameText.text = badgeInfo.name;

            imageController?.SetImage(DefaultBadgeSprite);
            if (!string.IsNullOrEmpty(badgeInfo.image))
                imageController?.RequestImage(badgeInfo.image, hideImageWhileLoading: true);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            SetBadgeNameToastActive(true);

        public void OnPointerExit(PointerEventData eventData) =>
            SetBadgeNameToastActive(false);

        private void SetBadgeNameToastActive(bool isActive) =>
            badgeNameTooltip.SetActive(isActive);
    }
}
