using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    /// <summary>
    ///     One pooled cart row. The click callbacks are plain Action fields assigned once when the row is created,
    ///     so rebinding never churns listeners. A hovered row brightens its background and shows the favorite and
    ///     remove buttons.
    /// </summary>
    public class ShopCartLineView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] public Image Background { get; private set; } = null!;
        [field: SerializeField] public Color NormalBackgroundColor { get; private set; } = new (1f, 1f, 1f, 0.1f);
        [field: SerializeField] public Color HoveredBackgroundColor { get; private set; } = new (1f, 1f, 1f, 0.2f);
        [field: SerializeField] public Image Thumbnail { get; private set; } = null!;
        [field: SerializeField] public Image RarityBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreatorText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceCreditsText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text QuantityText { get; private set; } = null!;
        [field: SerializeField] public GameObject QuantityStepper { get; private set; } = null!;
        [field: SerializeField] public Button IncrementButton { get; private set; } = null!;
        [field: SerializeField] public Button DecrementButton { get; private set; } = null!;
        [field: SerializeField] public Button RemoveButton { get; private set; } = null!;
        [field: SerializeField] public Button FavoriteButton { get; private set; } = null!;
        [field: SerializeField] public GameObject FavoriteOnIcon { get; private set; } = null!;
        [field: SerializeField] public GameObject FavoriteOffIcon { get; private set; } = null!;

        public Action<ShopCartLineView>? IncrementClicked;
        public Action<ShopCartLineView>? DecrementClicked;
        public Action<ShopCartLineView>? RemoveClicked;
        public Action<ShopCartLineView>? FavoriteClicked;

        private bool isHovered;
        private bool canBeFavorite;

        public string? BoundLineId { get; private set; }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void Awake()
        {
            IncrementButton.onClick.AddListener(() => IncrementClicked?.Invoke(this));
            DecrementButton.onClick.AddListener(() => DecrementClicked?.Invoke(this));
            RemoveButton.onClick.AddListener(() => RemoveClicked?.Invoke(this));
            FavoriteButton.onClick.AddListener(() => FavoriteClicked?.Invoke(this));
            RefreshHoverState();
        }

        // A row disabled under the pointer never receives the exit event.
        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDisable()
        {
            isHovered = false;
            RefreshHoverState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshHoverState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            RefreshHoverState();
        }

        public void Bind(ShopCartLine line, Sprite? thumbnail, Sprite? rarityBackground)
        {
            BoundLineId = line.Id;
            NameText.text = line.Listing.name;
            CreatorText.text = line.CreatorName ?? string.Empty;
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

        /// <summary>A line that cannot be favorited never shows the favorite button, hovered or not.</summary>
        public void SetFavorite(bool available, bool isFavorite)
        {
            canBeFavorite = available;
            FavoriteOnIcon.SetActive(isFavorite);
            FavoriteOffIcon.SetActive(!isFavorite);
            RefreshHoverState();
        }

        public void Unbind() =>
            BoundLineId = null;

        private void RefreshHoverState()
        {
            Background.color = isHovered ? HoveredBackgroundColor : NormalBackgroundColor;
            RemoveButton.gameObject.SetActive(isHovered);
            FavoriteButton.gameObject.SetActive(isHovered && canBeFavorite);
        }
    }
}
