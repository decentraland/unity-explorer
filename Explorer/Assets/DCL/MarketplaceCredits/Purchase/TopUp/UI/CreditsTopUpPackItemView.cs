using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpPackItemView : MonoBehaviour
    {
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

        private void OnDestroy() =>
            imageController?.Dispose();
    }
}
