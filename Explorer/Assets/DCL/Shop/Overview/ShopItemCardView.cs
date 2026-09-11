using DCL.Audio;
using DCL.Backpack.Gifting.Views;
using DCL.Communities;
using DCL.MarketplaceCredits.Purchase;
using DCL.UI;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Shop
{
    [Flags]
    public enum ShopCardActions
    {
        None = 0,
        View = 1,
        AddToCart = 2,
        Buy = 4,
    }

    public class ShopItemCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_SCALE = 1.025f;
        private const float HOVER_DURATION = 0.15f;
        private const string ADD_TO_CART_LABEL = "Add to cart";
        private const string IN_CART_LABEL = "In cart";

        [field: SerializeField] public RectTransform ScaleRoot { get; private set; } = null!;

        [field: Header("Media")]
        [field: SerializeField] public ImageView Thumbnail { get; private set; } = null!;
        [field: SerializeField] public Sprite? DefaultThumbnail { get; private set; }
        [field: SerializeField] public Image RarityBackground { get; private set; } = null!;

        [field: Header("Texts")]
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreatorText { get; private set; } = null!;

        [field: Header("Price")]
        [field: SerializeField] public GameObject PriceContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceText { get; private set; } = null!;
        [field: SerializeField] public GameObject NotForSaleTag { get; private set; } = null!;
        [field: SerializeField] public GameObject SaleContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SaleNowText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SaleWasText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SaleCountdownText { get; private set; } = null!;
        [field: SerializeField] public GameObject SaleBadge { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SaleBadgeText { get; private set; } = null!;

        [field: Header("Chips")]
        [field: SerializeField] public CanvasGroup ChipsGroup { get; private set; } = null!;
        [field: SerializeField] public Image RarityChipBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text RarityChipText { get; private set; } = null!;
        [field: SerializeField] public GameObject SmartChip { get; private set; } = null!;
        [field: SerializeField] public GameObject CategoryChip { get; private set; } = null!;
        [field: SerializeField] public Image CategoryIcon { get; private set; } = null!;
        [field: SerializeField] public GameObject GenderChip { get; private set; } = null!;
        [field: SerializeField] public Image GenderIcon { get; private set; } = null!;
        [field: SerializeField] public Sprite? MaleIcon { get; private set; }
        [field: SerializeField] public Sprite? FemaleIcon { get; private set; }
        [field: SerializeField] public Sprite? UnisexIcon { get; private set; }

        [field: Header("Actions")]
        [field: SerializeField] public CanvasGroup ActionsGroup { get; private set; } = null!;
        [field: SerializeField] public Button AddToCartButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text AddToCartText { get; private set; } = null!;
        [field: SerializeField] public Button BuyButton { get; private set; } = null!;
        [field: SerializeField] public Button ViewButton { get; private set; } = null!;
        [field: SerializeField] public Button ViewIconButton { get; private set; } = null!;
        [field: SerializeField] public GameObject ResolvingSpinner { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig? HoverAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig? ClickAudio { get; private set; }

        public Action<ShopItemCardView>? AddToCartClicked;
        public Action<ShopItemCardView>? BuyClicked;
        public Action<ShopItemCardView>? ViewClicked;

        private Tweener? scaleTween;
        private Tweener? chipsTween;
        private Tweener? actionsTween;
        private CancellationTokenSource? loadingThumbnailCts;
        private ShopCardActions actions;
        private bool cartFull;

        public ShopItemCardModel? Model { get; private set; }

        public bool IsResolving { get; private set; }

        private void Awake()
        {
            AddToCartButton.onClick.AddListener(() => OnActionClicked(AddToCartClicked));
            BuyButton.onClick.AddListener(() => OnActionClicked(BuyClicked));
            ViewButton.onClick.AddListener(() => OnActionClicked(ViewClicked));
            ViewIconButton.onClick.AddListener(() => OnActionClicked(ViewClicked));
        }

        private void OnEnable() =>
            ResetHoverVisuals();

        private void OnDisable()
        {
            loadingThumbnailCts.SafeCancelAndDispose();
            loadingThumbnailCts = null;
            ResetHoverVisuals();
        }

        public void Bind(ShopItemCardModel model, string creatorName, in GiftItemStyleSnapshot style, ThumbnailLoader thumbnailLoader,
            ShopCardActions cardActions, bool inCart, bool isCartFull, long nowUnixSeconds)
        {
            Model = model;
            IsResolving = false;

            NameText.text = model.Name;
            CreatorText.text = creatorName;

            if (style.rarityBackground != null)
                RarityBackground.sprite = style.rarityBackground;

            RarityChipBackground.color = style.flapColor;
            RarityChipText.text = model.Rarity;
            CategoryChip.SetActive(style.categoryIcon != null);

            if (style.categoryIcon != null)
                CategoryIcon.sprite = style.categoryIcon;

            SmartChip.SetActive(model.IsSmart);
            BindGender(model.Gender);
            RefreshSaleCountdown(nowUnixSeconds);

            loadingThumbnailCts = loadingThumbnailCts.SafeRestart();
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(model.ThumbnailUrl, Thumbnail, DefaultThumbnail, loadingThumbnailCts.Token, false).Forget();

            SetActions(cardActions);
            SetCartState(inCart, isCartFull);
            ResolvingSpinner.SetActive(false);
        }

        public void SetCreatorName(string creatorName) =>
            CreatorText.text = creatorName;

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

        public void RefreshSaleCountdown(long nowUnixSeconds)
        {
            if (Model == null)
                return;

            bool notForSale = Model.IsNotForSale;
            bool sale = !notForSale && Model.IsSaleActive(nowUnixSeconds);

            NotForSaleTag.SetActive(notForSale);
            PriceContainer.SetActive(!notForSale && !sale);
            SaleContainer.SetActive(sale);
            SaleBadge.SetActive(sale);

            if (notForSale)
                return;

            PriceText.text = Model.PriceCredits.ToString();

            if (!sale)
                return;

            SaleNowText.text = Model.PriceCredits.ToString();
            SaleWasText.text = Model.CompareAtCredits!.Value.ToString();
            SaleBadgeText.text = $"-{Model.DiscountPercent()}%";

            SaleCountdownText.text = Model.SaleEndsAtUnixSeconds.HasValue
                ? ShopItemCardModel.FormatCountdown(Model.SaleEndsAtUnixSeconds.Value - nowUnixSeconds)
                : string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (actions == ShopCardActions.None)
                return;

            if (HoverAudio != null)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);

            AnimateHover(true);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            AnimateHover(false);

        private void BindGender(string? gender)
        {
            Sprite? icon = gender switch
            {
                CatalogItemDtoExtensions.GENDER_MALE => MaleIcon,
                CatalogItemDtoExtensions.GENDER_FEMALE => FemaleIcon,
                CatalogItemDtoExtensions.GENDER_UNISEX => UnisexIcon,
                _ => null,
            };

            GenderChip.SetActive(icon != null);

            if (icon != null)
                GenderIcon.sprite = icon;
        }

        private void SetActions(ShopCardActions cardActions)
        {
            actions = cardActions;
            bool viewOnly = cardActions == ShopCardActions.View;

            AddToCartButton.gameObject.SetActive((cardActions & ShopCardActions.AddToCart) != 0);
            BuyButton.gameObject.SetActive((cardActions & ShopCardActions.Buy) != 0);
            ViewButton.gameObject.SetActive(viewOnly);
            ViewIconButton.gameObject.SetActive(!viewOnly && (cardActions & ShopCardActions.View) != 0);
        }

        private void OnActionClicked(Action<ShopItemCardView>? action)
        {
            if (ClickAudio != null)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);

            action?.Invoke(this);
        }

        private void AnimateHover(bool hovered)
        {
            KillTweens();

            scaleTween = ScaleRoot.DOScale(hovered ? HOVER_SCALE : 1f, HOVER_DURATION).SetEase(Ease.OutQuad);
            chipsTween = ChipsGroup.DOFade(hovered ? 0f : 1f, HOVER_DURATION).SetEase(Ease.OutQuad);
            actionsTween = ActionsGroup.DOFade(hovered ? 1f : 0f, HOVER_DURATION).SetEase(Ease.OutQuad);

            ActionsGroup.blocksRaycasts = hovered;
            ActionsGroup.interactable = hovered;
            ChipsGroup.blocksRaycasts = !hovered;
        }

        private void ResetHoverVisuals()
        {
            KillTweens();
            ScaleRoot.localScale = Vector3.one;
            ChipsGroup.alpha = 1f;
            ChipsGroup.blocksRaycasts = true;
            ActionsGroup.alpha = 0f;
            ActionsGroup.blocksRaycasts = false;
            ActionsGroup.interactable = false;
        }

        private void KillTweens()
        {
            scaleTween?.Kill();
            chipsTween?.Kill();
            actionsTween?.Kill();
        }
    }
}
