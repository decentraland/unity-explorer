using DCL.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpPackItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_SCALE = 1.05f;
        private const float HOVER_DURATION = 0.15f;

        [field: SerializeField] public Button BuyButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject BestValueBadge { get; private set; } = null!;
        [field: SerializeField] public ImageView PackImage { get; private set; } = null!;
        [field: SerializeField] public Sprite DefaultPackSprite { get; private set; } = null!;

        private ImageController? imageController;

        public void ConfigureImageController(ImageControllerProvider imageControllerProvider) =>
            imageController ??= imageControllerProvider.Create(PackImage);

        public void SetupImage(string imageUrl)
        {
            imageController?.SetImage(DefaultPackSprite);

            if (imageController != null && !string.IsNullOrEmpty(imageUrl))
                imageController.RequestImage(imageUrl, hideImageWhileLoading: true);
        }

        public void StopLoadingImage() =>
            imageController?.StopLoading();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (BuyButton.isActiveAndEnabled && BuyButton.interactable)
                AnimateScale(HOVER_SCALE);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            AnimateScale(1f);

        private void OnDisable()
        {
            // A card hidden under the pointer is never told the pointer left
            transform.DOKill();
            transform.localScale = Vector3.one;
        }

        private void OnDestroy() =>
            imageController?.Dispose();

        private void AnimateScale(float scale)
        {
            transform.DOKill();
            transform.DOScale(scale, HOVER_DURATION).SetEase(Ease.OutQuad);
        }
    }
}
