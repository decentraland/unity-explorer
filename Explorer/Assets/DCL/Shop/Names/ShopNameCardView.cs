using DCL.Audio;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Optional serialized references: Unity writes the backing field, so the private setter has no caller
// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.Shop
{
    public class ShopNameCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float HOVER_SCALE = 1.025f;
        private const float HOVER_DURATION = 0.15f;

        [field: SerializeField] public RectTransform ScaleRoot { get; private set; } = null!;
        [field: SerializeField] public TMP_Text TileNameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text NameText { get; private set; } = null!;
        [field: SerializeField] public TMP_Text PriceText { get; private set; } = null!;
        [field: SerializeField] public GameObject SelectedOutline { get; private set; } = null!;

        [field: Header("Actions")]
        [field: SerializeField] public CanvasGroup DetailsGroup { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup ActionsGroup { get; private set; } = null!;
        [field: SerializeField] public Button ViewButton { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig? HoverAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig? ClickAudio { get; private set; }

        public Action<ShopNameCardView>? Clicked;
        public Action<ShopNameCardView>? ViewClicked;

        private Tweener? scaleTween;
        private Tweener? actionsTween;
        private Tweener? detailsTween;

        public ShopNameCardModel? Model { get; private set; }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void Awake()
        {
            ViewButton.onClick.AddListener(() => OnActionClicked(ViewClicked));
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnEnable()
        {
            ResetHoverVisuals();
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDisable()
        {
            ResetHoverVisuals();
        }

        public void Bind(ShopNameCardModel model)
        {
            Model = model;
            TileNameText.text = model.Name;
            NameText.text = model.Name;
            PriceText.text = model.PriceText;
            SelectedOutline.SetActive(false);
        }

        public void SetSelected(bool selected) =>
            SelectedOutline.SetActive(selected);

        public void OnPointerEnter(PointerEventData eventData)
        {
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

        private void OnActionClicked(Action<ShopNameCardView>? action)
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
