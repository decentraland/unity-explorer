using DCL.Audio;
using DCL.Backpack.Gifting.Views;
using DCL.Communities;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

// Optional serialized references: Unity writes the backing field, so the private setter has no caller
// ReSharper disable UnusedAutoPropertyAccessor.Local
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

    public class ShopItemCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float HOVER_SCALE = 1.025f;
        private const float HOVER_DURATION = 0.15f;

        [field: SerializeField] public RectTransform ScaleRoot { get; private set; } = null!;

        [field: Header("Media")]
        [field: SerializeField] public ImageView Thumbnail { get; private set; } = null!;
        [field: SerializeField] public Sprite? DefaultThumbnail { get; private set; }
        [field: SerializeField] public Image RarityBackground { get; private set; } = null!;

        [field: Header("Texts")]
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CreatorText { get; private set; } = null!;
        [field: SerializeField] public ProfilePictureView CreatorPicture { get; private set; } = null!;

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
        [field: SerializeField] public Image RarityChipBackground { get; private set; } = null!;
        [field: SerializeField] public TMP_Text RarityChipText { get; private set; } = null!;
        [field: SerializeField] public GameObject CategoryFlap { get; private set; } = null!;
        [field: SerializeField] public Image CategoryFlapBackground { get; private set; } = null!;
        [field: SerializeField] public Image CategoryIcon { get; private set; } = null!;
        [field: SerializeField] public GameObject SelectedOutline { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public CanvasGroup DetailsGroup { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup ActionsGroup { get; private set; } = null!;
        [field: SerializeField] public Button AddToCartButton { get; private set; } = null!;
        [field: SerializeField] public Button BuyButton { get; private set; } = null!;
        [field: SerializeField] public GameObject ResolvingSpinner { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig? HoverAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig? ClickAudio { get; private set; }

        public Action<ShopItemCardView>? AddToCartClicked;
        public Action<ShopItemCardView>? BuyClicked;
        public Action<ShopItemCardView>? Clicked;

        private Tweener? scaleTween;
        private Tweener? actionsTween;
        private Tweener? detailsTween;
        private CancellationTokenSource? loadingThumbnailCts;
        private ShopCardActions actions;
        private bool cartFull;

        public ShopItemCardModel? Model { get; private set; }

        public bool IsResolving { get; private set; }

        private bool hasHoverActions => (actions & (ShopCardActions.AddToCart | ShopCardActions.Buy)) != 0;

        private void Awake()
        {
            AddToCartButton.onClick.AddListener(() => OnActionClicked(AddToCartClicked));
            BuyButton.onClick.AddListener(() => OnActionClicked(BuyClicked));
        }

        private void OnEnable() =>
            ResetHoverVisuals();

        private void OnDestroy() =>
            CreatorPicture.Dispose();

        private void OnDisable()
        {
            loadingThumbnailCts.SafeCancelAndDispose();
            loadingThumbnailCts = null;
            ResetHoverVisuals();
        }

        public void Bind(ShopItemCardModel model, string creatorName, IReactiveProperty<ProfileThumbnailViewModel> creatorThumbnail, in GiftItemStyleSnapshot style,
            ThumbnailLoader thumbnailLoader,            ShopCardActions cardActions, bool isCartFull, long nowUnixSeconds)
        {
            Model = model;
            IsResolving = false;

            NameText.text = model.Name;
            CreatorText.text = creatorName;
            CreatorPicture.Bind(creatorThumbnail);

            if (style.rarityBackground != null)
                RarityBackground.sprite = style.rarityBackground;

            RarityChipBackground.color = style.flapColor;
            RarityChipText.text = model.Rarity.ToUpperInvariant();
            CategoryFlap.SetActive(style.categoryIcon != null);
            CategoryFlapBackground.color = style.flapColor;

            if (style.categoryIcon != null)
                CategoryIcon.sprite = style.categoryIcon;

            RefreshSaleCountdown(nowUnixSeconds);

            loadingThumbnailCts = loadingThumbnailCts.SafeRestart();
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(model.ThumbnailUrl, Thumbnail, DefaultThumbnail, loadingThumbnailCts.Token, false).Forget();

            SetActions(cardActions);
            SetCartState(isCartFull);
            SelectedOutline.SetActive(false);
            ResolvingSpinner.SetActive(false);
        }

        public void SetCreatorName(string creatorName) =>
            CreatorText.text = creatorName;

        public void SetSelected(bool selected) =>
            SelectedOutline.SetActive(selected);

        public void SetCartState(bool isCartFull)
        {
            cartFull = isCartFull;
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
            if (!hasHoverActions)
                return;

            if (HoverAudio != null)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);

            AnimateHover(true);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            AnimateHover(false);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
                return;

            OnActionClicked(Clicked);
        }

        private void SetActions(ShopCardActions cardActions)
        {
            actions = cardActions;
            AddToCartButton.gameObject.SetActive((cardActions & ShopCardActions.AddToCart) != 0);
            BuyButton.gameObject.SetActive((cardActions & ShopCardActions.Buy) != 0);
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
            actionsTween = ActionsGroup.DOFade(hovered ? 1f : 0f, HOVER_DURATION).SetEase(Ease.OutQuad);
            detailsTween = DetailsGroup.DOFade(hovered ? 0f : 1f, HOVER_DURATION).SetEase(Ease.OutQuad);

            ActionsGroup.blocksRaycasts = hovered;
            ActionsGroup.interactable = hovered;
        }

        private void ResetHoverVisuals()
        {
            KillTweens();
            ScaleRoot.localScale = Vector3.one;
            DetailsGroup.alpha = 1f;
            ActionsGroup.alpha = 0f;
            ActionsGroup.blocksRaycasts = false;
            ActionsGroup.interactable = false;
        }

        private void KillTweens()
        {
            scaleTween?.Kill();
            actionsTween?.Kill();
            detailsTween?.Kill();
        }
    }
}
