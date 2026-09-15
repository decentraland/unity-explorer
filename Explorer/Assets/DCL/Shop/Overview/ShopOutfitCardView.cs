using DCL.Communities;
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
    public class ShopOutfitCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_SCALE = 1.025f;
        private const float HOVER_DURATION = 0.2f;
        private const string ADD_LABEL = "Add to cart";
        private const string ADDING_LABEL = "Adding...";
        private const string ITEMS_SINGULAR = "1 item";
        private const string ITEMS_FORMAT = "{0} items";

        [field: SerializeField] public RectTransform ScaleRoot { get; private set; } = null!;
        [field: SerializeField] public Image GradientBase { get; private set; } = null!;
        [field: SerializeField] public Image GradientCore { get; private set; } = null!;
        [field: SerializeField] public ImageView Thumbnail { get; private set; } = null!;
        [field: SerializeField] public Sprite? DefaultThumbnail { get; private set; }
        [field: SerializeField] public CanvasGroup RevealGroup { get; private set; } = null!;
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ItemsCountText { get; private set; } = null!;
        [field: SerializeField] public GameObject PriceContainer { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceText { get; private set; } = null!;
        [field: SerializeField] public GameObject PriceSkeleton { get; private set; } = null!;
        [field: SerializeField] public Button AddButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text AddButtonText { get; private set; } = null!;
        [field: SerializeField] public GameObject AddSpinner { get; private set; } = null!;

        public Action<ShopOutfitCardView>? AddClicked;

        private Tweener? scaleTween;
        private Tweener? revealTween;
        private CancellationTokenSource? loadingThumbnailCts;
        private bool ctaEnabled;

        public ShopOutfitModel? Model { get; private set; }

        public bool IsAdding { get; private set; }

        private void Awake() =>
            AddButton.onClick.AddListener(() => AddClicked?.Invoke(this));

        private void OnEnable() =>
            ResetHoverVisuals();

        private void OnDisable()
        {
            loadingThumbnailCts.SafeCancelAndDispose();
            loadingThumbnailCts = null;
            ResetHoverVisuals();
        }

        public void Bind(ShopOutfitModel model, ThumbnailLoader thumbnailLoader, bool resolutionFailed)
        {
            Model = model;
            GradientBase.color = model.GradientFrom;
            GradientCore.color = model.GradientTo;
            NameText.text = model.Name;
            int itemCount = model.Outfit.items.Length;
            ItemsCountText.text = itemCount == 1 ? ITEMS_SINGULAR : string.Format(ITEMS_FORMAT, itemCount);
            PriceContainer.SetActive(!resolutionFailed);
            PriceSkeleton.SetActive(resolutionFailed);
            PriceText.text = model.TotalCredits.ToString();
            AddButton.gameObject.SetActive(!resolutionFailed);
            SetAdding(false);

            loadingThumbnailCts = loadingThumbnailCts.SafeRestart();
            ShopThumbnails.LoadWithRetryAsync(thumbnailLoader, model.ThumbnailUrl, Thumbnail, DefaultThumbnail, loadingThumbnailCts.Token).Forget();
        }

        public void SetCtaEnabled(bool isEnabled)
        {
            ctaEnabled = isEnabled;
            AddButton.interactable = isEnabled && !IsAdding;
        }

        public void SetAdding(bool adding)
        {
            IsAdding = adding;
            AddSpinner.SetActive(adding);
            AddButtonText.text = adding ? ADDING_LABEL : ADD_LABEL;
            AddButton.interactable = ctaEnabled && !adding;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            AnimateHover(true);

        public void OnPointerExit(PointerEventData eventData) =>
            AnimateHover(false);

        private void AnimateHover(bool hovered)
        {
            KillTweens();
            scaleTween = ScaleRoot.DOScale(hovered ? HOVER_SCALE : 1f, HOVER_DURATION).SetEase(Ease.OutQuad);
            revealTween = RevealGroup.DOFade(hovered ? 1f : 0f, HOVER_DURATION).SetEase(Ease.OutQuad);
            RevealGroup.blocksRaycasts = hovered;
            RevealGroup.interactable = hovered;
        }

        private void ResetHoverVisuals()
        {
            KillTweens();
            ScaleRoot.localScale = Vector3.one;
            RevealGroup.alpha = 0f;
            RevealGroup.blocksRaycasts = false;
            RevealGroup.interactable = false;
        }

        private void KillTweens()
        {
            scaleTween?.Kill();
            revealTween?.Kill();
        }
    }
}
