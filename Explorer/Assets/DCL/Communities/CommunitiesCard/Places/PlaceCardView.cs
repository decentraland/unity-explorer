using DCL.UI;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;
using PlaceData = DCL.Communities.CommunitiesCard.Places.PlacesSectionController.PlaceData;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlaceCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_ANIMATION_DURATION = 0.3f;
        private const float HOVER_ANIMATION_HEIGHT_TO_APPLY = 112f;

        [SerializeField] private RectTransform headerContainer = null!;
        [SerializeField] private RectTransform footerContainer = null!;
        [SerializeField] private CanvasGroup interactionButtonsCanvasGroup = null!;
        [SerializeField] private Sprite defaultPlaceThumbnail = null!;

        [Header("Place info")]
        [SerializeField] private ImageView placeThumbnailImage = null!;
        [SerializeField] private TMP_Text onlineMembersText = null!;
        [SerializeField] private TMP_Text placeNameText = null!;
        [SerializeField] private TMP_Text placeDescriptionText = null!;
        [SerializeField] private TMP_Text placeCoordsText = null!;

        [Header("Buttons")]
        [SerializeField] private ToggleView likeToggle = null!;
        [SerializeField] private ToggleView dislikeToggle = null!;
        [SerializeField] private ToggleView favoriteToggle = null!;
        [SerializeField] private Button shareButton = null!;
        [SerializeField] private Button infoButton = null!;
        [SerializeField] private Button jumpInButton = null!;
        [SerializeField] private Button deleteButton = null!;

        private Tweener? headerTween;
        private Tweener? footerTween;
        private Tweener? descriptionTween;
        private Vector2 originalHeaderSizeDelta;
        private Vector2 originalFooterSizeDelta;

        private PlaceInfo? currentPlaceInfo;

        public event Action<PlaceInfo, bool, PlaceCardView>? LikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView>? DislikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView>? FavoriteToggleChanged;
        public event Action<PlaceInfo, Vector2, PlaceCardView>? ShareButtonClicked;
        public event Action<PlaceInfo>? InfoButtonClicked;
        public event Action<PlaceInfo>? JumpInButtonClicked;
        public event Action<PlaceInfo>? DeleteButtonClicked;

        private bool canPlayUnHoverAnimation = true;
        // This is used to control whether the un-hover animation can be played or not when the user exits the card because the context menu is opened.
        internal bool CanPlayUnHoverAnimation
        {
            get => canPlayUnHoverAnimation;
            set
            {
                if (!canPlayUnHoverAnimation && value)
                {
                    canPlayUnHoverAnimation = value;
                    PlayHoverExitAnimation();
                }
                canPlayUnHoverAnimation = value;
            }
        }

        private void Awake()
        {
            originalHeaderSizeDelta = headerContainer.sizeDelta;
            originalFooterSizeDelta = footerContainer.sizeDelta;

            likeToggle.Toggle.onValueChanged.AddListener(value => LikeToggleChanged?.Invoke(currentPlaceInfo!, value, this));
            dislikeToggle.Toggle.onValueChanged.AddListener(value => DislikeToggleChanged?.Invoke(currentPlaceInfo!, value, this));
            favoriteToggle.Toggle.onValueChanged.AddListener(value => FavoriteToggleChanged?.Invoke(currentPlaceInfo!, value, this));
            shareButton.onClick.AddListener(() => ShareButtonClicked?.Invoke(currentPlaceInfo!, shareButton.transform.position, this));
            infoButton.onClick.AddListener(() => InfoButtonClicked?.Invoke(currentPlaceInfo!));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(currentPlaceInfo!));
            deleteButton.onClick.AddListener(() => DeleteButtonClicked?.Invoke(currentPlaceInfo!));
        }

        private void OnEnable() =>
            PlayHoverExitAnimation(instant: true);

        public void Configure(PlaceData placeInfo, bool userOwnsPlace, ThumbnailLoader thumbnailLoader, CancellationToken ct)
        {
            currentPlaceInfo = placeInfo.PlaceInfo;

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(placeInfo.PlaceInfo.image, placeThumbnailImage, defaultPlaceThumbnail, ct, true).Forget();

            placeNameText.text = placeInfo.PlaceInfo.title;
            placeDescriptionText.text = placeInfo.OwnerName;
            onlineMembersText.text = $"{placeInfo.PlaceInfo.user_count}";
            placeCoordsText.text = string.IsNullOrWhiteSpace(placeInfo.PlaceInfo.world_name) ? placeInfo.PlaceInfo.base_position : placeInfo.PlaceInfo.world_name;

            deleteButton.gameObject.SetActive(userOwnsPlace);

            //Make sure to remove listeners before setting values in order to avoid unwanted calls to previously subscribed methods
            LikeToggleChanged = null;
            DislikeToggleChanged = null;
            FavoriteToggleChanged = null;

            likeToggle.Toggle.isOn = placeInfo.PlaceInfo.user_like;
            dislikeToggle.Toggle.isOn = placeInfo.PlaceInfo.user_dislike;
            favoriteToggle.Toggle.isOn = placeInfo.PlaceInfo.user_favorite;
        }

        public void SubscribeToInteractions(Action<PlaceInfo, bool, PlaceCardView> likeToggleChanged,
            Action<PlaceInfo, bool, PlaceCardView> dislikeToggleChanged,
            Action<PlaceInfo, bool, PlaceCardView> favoriteToggleChanged,
            Action<PlaceInfo, Vector2, PlaceCardView> shareButtonClicked,
            Action<PlaceInfo> infoButtonClicked,
            Action<PlaceInfo> jumpInButtonClicked,
            Action<PlaceInfo> deleteButtonClicked)
        {
            LikeToggleChanged = null;
            DislikeToggleChanged = null;
            FavoriteToggleChanged = null;
            ShareButtonClicked = null;
            InfoButtonClicked = null;
            JumpInButtonClicked = null;
            DeleteButtonClicked = null;

            LikeToggleChanged += likeToggleChanged;
            DislikeToggleChanged += dislikeToggleChanged;
            FavoriteToggleChanged += favoriteToggleChanged;
            ShareButtonClicked += shareButtonClicked;
            InfoButtonClicked += infoButtonClicked;
            JumpInButtonClicked += jumpInButtonClicked;
            DeleteButtonClicked += deleteButtonClicked;
        }

        public void SilentlySetLikeToggle(bool isOn)
        {
            likeToggle.Toggle.SetIsOnWithoutNotify(isOn);
            likeToggle.SetToggleGraphics(isOn);
        }

        public void SilentlySetDislikeToggle(bool isOn)
        {
            dislikeToggle.Toggle.SetIsOnWithoutNotify(isOn);
            dislikeToggle.SetToggleGraphics(isOn);
        }

        public void SilentlySetFavoriteToggle(bool isOn)
        {
            favoriteToggle.Toggle.SetIsOnWithoutNotify(isOn);
            favoriteToggle.SetToggleGraphics(isOn);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PlayHoverAnimation();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (canPlayUnHoverAnimation)
                PlayHoverExitAnimation();
        }

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
