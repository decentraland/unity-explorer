using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlaceCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_ANIMATION_DURATION = 0.3f;
        private const float HOVER_ANIMATION_HEIGHT_TO_APPLY = 106f;

        [SerializeField] private RectTransform headerContainer;
        [SerializeField] private RectTransform footerContainer;
        [SerializeField] private CanvasGroup interactionButtonsCanvasGroup;

        [Header("Place info")]
        [SerializeField] private TMP_Text onlineMembersText;
        [SerializeField] private TMP_Text placeNameText;
        [SerializeField] private TMP_Text placeDescriptionText;

        [Header("Buttons")]
        [SerializeField] private Toggle likeToggle;
        [SerializeField] private Toggle dislikeToggle;
        [SerializeField] private Toggle favoriteToggle;
        [SerializeField] private Button shareButton;
        [SerializeField] private Button infoButton;
        [SerializeField] private Button jumpInButton;

        private Tweener headerTween;
        private Tweener footerTween;
        private Tweener descriptionTween;
        private Vector2 originalHeaderSizeDelta;
        private Vector2 originalFooterSizeDelta;

        private PlaceInfo currentPlaceInfo;

        public event Action<PlaceInfo, bool> LikeToggleChanged;
        public event Action<PlaceInfo, bool> DislikeToggleChanged;
        public event Action<PlaceInfo, bool> FavoriteToggleChanged;
        public event Action<PlaceInfo> ShareButtonClicked;
        public event Action<PlaceInfo> InfoButtonClicked;
        public event Action<PlaceInfo> JumpInButtonClicked;

        private void Awake()
        {
            originalHeaderSizeDelta = headerContainer.sizeDelta;
            originalFooterSizeDelta = footerContainer.sizeDelta;

            likeToggle.onValueChanged.AddListener(value => LikeToggleChanged?.Invoke(currentPlaceInfo, value));
            dislikeToggle.onValueChanged.AddListener(value => DislikeToggleChanged?.Invoke(currentPlaceInfo, value));
            favoriteToggle.onValueChanged.AddListener(value => FavoriteToggleChanged?.Invoke(currentPlaceInfo, value));
            shareButton.onClick.AddListener(() => ShareButtonClicked?.Invoke(currentPlaceInfo));
            infoButton.onClick.AddListener(() => InfoButtonClicked?.Invoke(currentPlaceInfo));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(currentPlaceInfo));
        }

        private void OnEnable() =>
            PlayHoverExitAnimation(instant: true);

        public void Configure(PlaceInfo placeInfo)
        {
            currentPlaceInfo = placeInfo;
        }

        public void SubscribeToInteractions(Action<PlaceInfo, bool> likeToggleChanged,
            Action<PlaceInfo, bool> dislikeToggleChanged,
            Action<PlaceInfo, bool> favoriteToggleChanged,
            Action<PlaceInfo> shareButtonClicked,
            Action<PlaceInfo> infoButtonClicked,
            Action<PlaceInfo> jumpInButtonClicked)
        {
            LikeToggleChanged = null;
            DislikeToggleChanged = null;
            FavoriteToggleChanged = null;
            ShareButtonClicked = null;
            InfoButtonClicked = null;
            JumpInButtonClicked = null;

            LikeToggleChanged += likeToggleChanged;
            DislikeToggleChanged += dislikeToggleChanged;
            FavoriteToggleChanged += favoriteToggleChanged;
            ShareButtonClicked += shareButtonClicked;
            InfoButtonClicked += infoButtonClicked;
            JumpInButtonClicked += jumpInButtonClicked;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PlayHoverAnimation();

        public void OnPointerExit(PointerEventData eventData) =>
            PlayHoverExitAnimation();

        private void PlayHoverAnimation()
        {
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            headerTween = DOTween.To(() =>
                          headerContainer.sizeDelta,
                          newSizeDelta => headerContainer.sizeDelta = newSizeDelta,
                          new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y - HOVER_ANIMATION_HEIGHT_TO_APPLY),
                          HOVER_ANIMATION_DURATION)
                     .SetEase(Ease.OutQuad);

            footerTween = DOTween.To(() =>
                          footerContainer.sizeDelta,
                          newSizeDelta => footerContainer.sizeDelta = newSizeDelta,
                          new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y + HOVER_ANIMATION_HEIGHT_TO_APPLY),
                          HOVER_ANIMATION_DURATION)
                     .SetEase(Ease.OutQuad);

            descriptionTween = DOTween.To(() =>
                               interactionButtonsCanvasGroup.alpha,
                               newAlpha => interactionButtonsCanvasGroup.alpha = newAlpha,
                               1f,
                               HOVER_ANIMATION_DURATION)
                          .SetEase(Ease.OutQuad);
        }

        private void PlayHoverExitAnimation(bool instant = false)
        {
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            if (instant)
            {
                headerContainer.sizeDelta = originalHeaderSizeDelta;
                footerContainer.sizeDelta = originalFooterSizeDelta;
                interactionButtonsCanvasGroup.alpha = 0f;
            }
            else
            {
                headerTween = DOTween.To(() =>
                              headerContainer.sizeDelta,
                              x => headerContainer.sizeDelta = x,
                              new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y),
                              HOVER_ANIMATION_DURATION)
                         .SetEase(Ease.OutQuad);

                footerTween = DOTween.To(() =>
                              footerContainer.sizeDelta,
                              x => footerContainer.sizeDelta = x,
                              new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y),
                              HOVER_ANIMATION_DURATION)
                         .SetEase(Ease.OutQuad);

                descriptionTween = DOTween.To(() =>
                                   interactionButtonsCanvasGroup.alpha,
                                   newAlpha => interactionButtonsCanvasGroup.alpha = newAlpha,
                                   0f,
                                   HOVER_ANIMATION_DURATION)
                              .SetEase(Ease.OutQuad);
            }
        }
    }
}
