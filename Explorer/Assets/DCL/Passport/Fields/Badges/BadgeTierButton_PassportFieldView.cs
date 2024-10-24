using DCL.BadgesAPIService;
using DCL.UI;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Passport.Fields.Badges
{
    public class BadgeTierButton_PassportFieldView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public Image BackgroundImage { get; private set; }

        [field: SerializeField]
        public Button Button { get; private set; }

        [field: SerializeField]
        public ImageView TierImage { get; private set; }

        [field: SerializeField]
        public Sprite DefaultTierSprite { get; private set; }

        [field: SerializeField]
        public GameObject SelectedOutline { get; private set; }

        [field: SerializeField]
        public Color NormalBackgroundColor { get; private set; }

        [field: SerializeField]
        public Color HoverBackgroundColor { get; private set; }

        [field: SerializeField]
        public Color LockedBadgeImageColor { get; private set; }

        [field: SerializeField]
        public Color NonLockedBadgeImageColor { get; private set; }

        public TierData Model { get; private set; }

        private ImageController? imageController;

        public void ConfigureImageController(IWebRequestController webRequestController, IGetTextureArgsFactory getTextureArgsFactory)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(TierImage, webRequestController, getTextureArgsFactory);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void SetAsSelected(bool isSelected) =>
            SelectedOutline.SetActive(isSelected);

        public void Setup(TierData tierData, string completedAt)
        {
            Model = tierData;
            SetupTierImage(tierData, completedAt);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            BackgroundImage.color = HoverBackgroundColor;

        public void OnPointerExit(PointerEventData eventData) =>
            BackgroundImage.color = NormalBackgroundColor;

        private void SetupTierImage(TierData tierData, string completedAt)
        {
            TierImage.SetColor(string.IsNullOrEmpty(completedAt) ? LockedBadgeImageColor : NonLockedBadgeImageColor);
            imageController?.SetImage(DefaultTierSprite);
            if (tierData.assets is { textures2d: not null } && !string.IsNullOrEmpty(tierData.assets.textures2d.normal))
                imageController?.RequestImage(tierData.assets.textures2d.normal, hideImageWhileLoading: true);
        }
    }
}
