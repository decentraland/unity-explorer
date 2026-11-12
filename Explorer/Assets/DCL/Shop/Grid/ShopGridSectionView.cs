using System;
using UnityEngine;
using UnityEngine.UI;

// Optional serialized references: Unity writes the backing field, so the private setter has no caller
// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.Shop
{
    public class ShopGridSectionView : ShopGridViewBase<ShopItemCardView, ShopItemCardModel>
    {
        private const string EMPTY_SEARCH_FORMAT = "No items found for '{0}'.";
        private const string EMPTY_FILTERS_TEXT = "No items match these filters.";

        [field: Header("Items")]
        [field: SerializeField] public ShopCategoryDropdownView? CategoryDropdown { get; private set; }
        [field: SerializeField] public Button BrowseButton { get; private set; } = null!;

        public event Action? BrowseClicked;
        public event Action<ShopItemCardView>? CardAddToCartClicked;
        public event Action<ShopItemCardView>? CardBuyClicked;
        public event Action<ShopItemCardView>? CardClicked;

        public void SetEmpty(bool on, string? query) =>
            ShowEmpty(on, query != null ? string.Format(EMPTY_SEARCH_FORMAT, query) : EMPTY_FILTERS_TEXT);

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        protected override void Awake()
        {
            base.Awake();
            BrowseButton.onClick.AddListener(() => BrowseClicked?.Invoke());
        }

        protected override void WireCard(ShopItemCardView card)
        {
            card.AddToCartClicked = c => CardAddToCartClicked?.Invoke(c);
            card.BuyClicked = c => CardBuyClicked?.Invoke(c);
            card.Clicked = c => CardClicked?.Invoke(c);
        }
    }
}
