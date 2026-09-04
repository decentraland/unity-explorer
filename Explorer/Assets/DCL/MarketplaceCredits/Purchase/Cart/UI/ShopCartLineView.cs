using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    /// <summary>
    ///     One pooled cart row. The click callbacks are plain Action fields assigned once when the row is created,
    ///     so rebinding never churns listeners.
    /// </summary>
    public class ShopCartLineView : MonoBehaviour
    {
        [field: SerializeField] public Image Thumbnail { get; private set; } = null!;
        [field: SerializeField] public Image RarityBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceCreditsText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text QuantityText { get; private set; } = null!;
        [field: SerializeField] public GameObject QuantityStepper { get; private set; } = null!;
        [field: SerializeField] public Button IncrementButton { get; private set; } = null!;
        [field: SerializeField] public Button DecrementButton { get; private set; } = null!;
        [field: SerializeField] public Button RemoveButton { get; private set; } = null!;

        public Action<ShopCartLineView>? IncrementClicked;
        public Action<ShopCartLineView>? DecrementClicked;
        public Action<ShopCartLineView>? RemoveClicked;

        public string? BoundLineId { get; private set; }

        private void Awake()
        {
            IncrementButton.onClick.AddListener(() => IncrementClicked?.Invoke(this));
            DecrementButton.onClick.AddListener(() => DecrementClicked?.Invoke(this));
            RemoveButton.onClick.AddListener(() => RemoveClicked?.Invoke(this));
        }

        public void Bind(ShopCartLine line, Sprite? thumbnail, Sprite? rarityBackground)
        {
            BoundLineId = line.Id;
            NameText.text = line.Listing.name;
            PriceCreditsText.text = line.TotalCredits.ToString();
            QuantityText.text = line.Quantity.ToString();
            QuantityStepper.SetActive(line.IsPrimary);
            IncrementButton.interactable = line.Quantity < line.StockCap;
            DecrementButton.interactable = line.Quantity > 1;

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
