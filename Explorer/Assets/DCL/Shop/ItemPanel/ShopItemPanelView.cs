using DCL.Backpack;
using DCL.UI;
using DCL.UI.ProfileElements;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

// Optional serialized references: Unity writes the backing field, so the private setter has no caller
// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.Shop
{
    public class ShopItemPanelView : MonoBehaviour
    {
        private const int HIDE_CHIPS_POOL_CAPACITY = 13;
        private const string ADD_TO_CART_LABEL = "Add to cart";
        private const string IN_CART_LABEL = "In cart";

        [field: Header("Header")]
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button FavoriteButton { get; private set; } = null!;
        [field: SerializeField] public GameObject FavoriteOnIcon { get; private set; } = null!;
        [field: SerializeField] public GameObject FavoriteOffIcon { get; private set; } = null!;

        [field: Header("Media")]
        [field: SerializeField] public ImageView Thumbnail { get; private set; } = null!;
        [field: SerializeField] public Sprite? DefaultThumbnail { get; private set; }
        [field: SerializeField] public Image RarityBackground { get; private set; } = null!;
        [field: SerializeField] public Image CategoryIcon { get; private set; } = null!;

        [field: Header("Info")]
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public GameObject PriceContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceText { get; private set; } = null!;
        [field: SerializeField] public GameObject NotForSaleTag { get; private set; } = null!;
        [field: SerializeField] public Image RarityChipBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text RarityChipText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreatorText { get; private set; } = null!;
        [field: SerializeField] public ProfilePictureView CreatorPicture { get; private set; } = null!;
        [field: SerializeField] public GameObject DescriptionContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text DescriptionText { get; private set; } = null!;
        [field: SerializeField] public GameObject PreviewUnavailableNote { get; private set; } = null!;
        [field: SerializeField] public GameObject HidesContainer { get; private set; } = null!;
        [field: SerializeField] public Transform HidesChipsParent { get; private set; } = null!;
        [field: SerializeField] public HideCategoryView HideChipPrefab { get; private set; } = null!;
        [field: SerializeField] public GameObject DetailsSpinner { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public Button AddToCartButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text AddToCartText { get; private set; } = null!;
        [field: SerializeField] public Button BuyButton { get; private set; } = null!;
        [field: SerializeField] public Button MarketplaceButton { get; private set; } = null!;
        [field: SerializeField] public GameObject ResolvingSpinner { get; private set; } = null!;

        private readonly List<HideCategoryView> activeHideChips = new ();
        private IObjectPool<HideCategoryView>? hideChipsPool;
        private bool cartFull;

        public bool IsResolving { get; private set; }

        public event Action? CloseClicked;
        public event Action? FavoriteClicked;
        public event Action? AddToCartClicked;
        public event Action? BuyClicked;
        public event Action? MarketplaceClicked;

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDestroy() =>
            CreatorPicture.Dispose();

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void Awake()
        {
            CloseButton.onClick.AddListener(() => CloseClicked?.Invoke());
            FavoriteButton.onClick.AddListener(() => FavoriteClicked?.Invoke());
            AddToCartButton.onClick.AddListener(() => AddToCartClicked?.Invoke());
            BuyButton.onClick.AddListener(() => BuyClicked?.Invoke());
            MarketplaceButton.onClick.AddListener(() => MarketplaceClicked?.Invoke());
        }

        public void SetFavoriteVisible(bool visible) =>
            FavoriteButton.gameObject.SetActive(visible);

        public void SetFavorite(bool isFavorite)
        {
            FavoriteOnIcon.SetActive(isFavorite);
            FavoriteOffIcon.SetActive(!isFavorite);
        }

        public void SetActions(ShopCardActions actions)
        {
            bool viewOnly = actions == ShopCardActions.View;
            AddToCartButton.gameObject.SetActive((actions & ShopCardActions.AddToCart) != 0);
            BuyButton.gameObject.SetActive((actions & ShopCardActions.Buy) != 0);
            MarketplaceButton.gameObject.SetActive(viewOnly);
        }

        public void SetCartState(bool inCart, bool isCartFull)
        {
            cartFull = isCartFull;
            AddToCartText.text = inCart ? IN_CART_LABEL : ADD_TO_CART_LABEL;
            AddToCartButton.interactable = !isCartFull && !IsResolving;
        }

        public void SetResolving(bool resolving)
        {
            IsResolving = resolving;
            ResolvingSpinner.SetActive(resolving);
            AddToCartButton.interactable = !resolving && !cartFull;
            BuyButton.interactable = !resolving;
        }

        public void SetPrice(bool notForSale, long priceCredits)
        {
            NotForSaleTag.SetActive(notForSale);
            PriceContainer.SetActive(!notForSale);
            PriceText.text = ShopItemCardModel.FormatExactPrice(priceCredits);
        }

        public void SetDetailsLoading(bool loading)
        {
            DetailsSpinner.SetActive(loading);

            if (!loading)
                return;

            DescriptionContainer.SetActive(false);
            PreviewUnavailableNote.SetActive(false);
            HidesContainer.SetActive(false);
            ClearHideChips();
        }

        public void SetDescription(string? description)
        {
            bool has = !string.IsNullOrWhiteSpace(description);
            DescriptionContainer.SetActive(has || PreviewUnavailableNote.activeSelf);
            DescriptionText.text = has ? description : string.Empty;
        }

        /// <summary>
        ///     The note flows under the description, so showing it also shows the description container.
        /// </summary>
        public void SetPreviewUnavailable(bool unavailable)
        {
            PreviewUnavailableNote.SetActive(unavailable);

            if (unavailable)
                DescriptionContainer.SetActive(true);
        }

        public void AddHideChip(string label, Sprite icon)
        {
            HideCategoryView chip = HideChipsPool().Get();
            activeHideChips.Add(chip);
            chip.transform.SetAsLastSibling();
            chip.categoryText.text = label;
            chip.categoryImage.sprite = icon;
            HidesContainer.SetActive(true);
        }

        public void ClearHideChips()
        {
            foreach (HideCategoryView chip in activeHideChips)
                HideChipsPool().Release(chip);

            activeHideChips.Clear();
        }

        private IObjectPool<HideCategoryView> HideChipsPool() =>
            hideChipsPool ??= new ObjectPool<HideCategoryView>(
                () => Instantiate(HideChipPrefab, HidesChipsParent),
                defaultCapacity: HIDE_CHIPS_POOL_CAPACITY,
                actionOnGet: chip => chip.gameObject.SetActive(true),
                actionOnRelease: chip => chip.gameObject.SetActive(false));
    }
}
