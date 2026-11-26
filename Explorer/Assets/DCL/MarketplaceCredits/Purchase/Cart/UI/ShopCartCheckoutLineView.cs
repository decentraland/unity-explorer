using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    /// <summary>
    ///     One pooled, read-only row of the checkout popup: the item, its creator, what the line costs at its
    ///     live price, and the quantity when it is more than one.
    /// </summary>
    public class ShopCartCheckoutLineView : MonoBehaviour
    {
        private const string QUANTITY_FORMAT = "x{0}";

        [field: SerializeField] public Image Thumbnail { get; private set; } = null!;
        [field: SerializeField] public Image RarityBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreatorText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceCreditsText { get; private set; } = null!;
        [field: SerializeField] public GameObject QuantityBadge { get; private set; } = null!;
        [field: SerializeField] public TMP_Text QuantityText { get; private set; } = null!;

        public string? BoundLineId { get; private set; }

        public void Bind(in ReviewedCartLine line, Sprite? thumbnail, Sprite? rarityBackground)
        {
            BoundLineId = line.Line.Id;
            NameText.text = line.Line.Listing.name;
            CreatorText.text = line.Line.CreatorName ?? string.Empty;
            PriceCreditsText.text = PaymentAmountFormat.Credits(line.TotalCredits);
            QuantityBadge.SetActive(line.Quantity > 1);
            QuantityText.text = string.Format(QUANTITY_FORMAT, line.Quantity);

            if (rarityBackground != null)
                RarityBackground.sprite = rarityBackground;

            SetThumbnail(thumbnail);
        }

        public void SetThumbnail(Sprite? thumbnail)
        {
            Thumbnail.enabled = thumbnail != null;

            if (thumbnail != null)
                Thumbnail.sprite = thumbnail;
        }

        public void Unbind() =>
            BoundLineId = null;
    }
}
