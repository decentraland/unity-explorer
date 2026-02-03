using DCL.Communities;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Places
{
    public class PlaceCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_ANIMATION_DURATION = 0.3f;
        private const float HOVER_ANIMATION_HEIGHT_TO_APPLY = 112f;
        private const string FEATURED_CATEGORY = "featured";

        [SerializeField] private RectTransform headerContainer = null!;
        [SerializeField] private RectTransform footerContainer = null!;
        [SerializeField] private CanvasGroup interactionButtonsCanvasGroup = null!;
        [SerializeField] private Sprite defaultPlaceThumbnail = null!;

        [Header("Place info")]
        [SerializeField] private ImageView placeThumbnailImage = null!;
        [SerializeField] private TMP_Text onlineMembersText = null!;
        [SerializeField] private TMP_Text placeNameText = null!;
        [SerializeField] private TMP_Text placeDescriptionText = null!;
        [SerializeField] private TMP_Text likeRateText = null!;
        [SerializeField] private TMP_Text placeCoordsText = null!;
        [SerializeField] private GameObject featuredTag = null!;
        [SerializeField] private GameObject liveTag = null!;
        [SerializeField] private FriendsConnectedConfig friendsConnected;

        [Header("Buttons")]
        [SerializeField] private ToggleView likeToggle = null!;
        [SerializeField] private ToggleView dislikeToggle = null!;
        [SerializeField] private ToggleView favoriteToggle = null!;
        [SerializeField] private ToggleView homeToggle = null!;
        [SerializeField] private Button shareButton = null!;
        [SerializeField] private Button infoButton = null!;
        [SerializeField] private Button jumpInButton = null!;
        [SerializeField] private Button deleteButton = null!;
        [SerializeField] private Button mainButton = null!;

        [Serializable]
        private struct FriendsConnectedConfig
        {
            public GameObject root;
            public FriendsConnectedThumbnail[] thumbnails;
            public GameObject amountContainer;
            public TMP_Text amountLabel;

            [Serializable]
            public struct FriendsConnectedThumbnail
            {
                public GameObject root;
                public ProfilePictureView picture;
            }
        }

        private Tweener? headerTween;
        private Tweener? footerTween;
        private Tweener? descriptionTween;
        private Vector2 originalHeaderSizeDelta;
        private Vector2 originalFooterSizeDelta;
        private PlaceInfo? currentPlaceInfo;
        private CancellationTokenSource loadingThumbnailCts;

        public event Action<PlaceInfo, bool, PlaceCardView>? LikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView>? DislikeToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView>? FavoriteToggleChanged;
        public event Action<PlaceInfo, bool, PlaceCardView>? HomeToggleChanged;
        public event Action<PlaceInfo, Vector2, PlaceCardView>? ShareButtonClicked;
        public event Action<PlaceInfo>? InfoButtonClicked;
        public event Action<PlaceInfo>? JumpInButtonClicked;
        public event Action<PlaceInfo>? DeleteButtonClicked;
        public event Action<PlaceInfo, PlaceCardView>? MainButtonClicked;

        public bool IsSetAsHome => homeToggle.Toggle.isOn;

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
            homeToggle.Toggle.onValueChanged.AddListener(value => HomeToggleChanged?.Invoke(currentPlaceInfo!, value, this));
            shareButton.onClick.AddListener(() => ShareButtonClicked?.Invoke(currentPlaceInfo!, shareButton.transform.position, this));
            infoButton.onClick.AddListener(() => InfoButtonClicked?.Invoke(currentPlaceInfo!));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(currentPlaceInfo!));
            deleteButton.onClick.AddListener(() => DeleteButtonClicked?.Invoke(currentPlaceInfo!));
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(currentPlaceInfo!, this));
        }

        private void OnEnable() =>
            PlayHoverExitAnimation(instant: true);

        private void OnDisable() =>
            loadingThumbnailCts.SafeCancelAndDispose();

        public void Configure(PlaceInfo placeInfo, string ownerName, bool userOwnsPlace, ThumbnailLoader thumbnailLoader,
            List<Profile.CompactInfo>? friends = null, ProfileRepositoryWrapper? profileRepositoryWrapper = null, bool isHome = false)
        {
            currentPlaceInfo = placeInfo;

            loadingThumbnailCts = loadingThumbnailCts.SafeRestart();
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(placeInfo.image, placeThumbnailImage, defaultPlaceThumbnail, loadingThumbnailCts.Token, true).Forget();

            placeNameText.text = placeInfo.title;
            placeDescriptionText.text = ownerName;
            onlineMembersText.text = $"{(placeInfo.connected_addresses != null ? placeInfo.connected_addresses.Length : placeInfo.user_count)}";
            likeRateText.text = $"{(placeInfo.like_rate_as_float ?? 0) * 100:F0}%";
            placeCoordsText.text = string.IsNullOrWhiteSpace(placeInfo.world_name) ? placeInfo.base_position : placeInfo.world_name;
            liveTag.SetActive(placeInfo.live);

            bool showFriendsConnected = friends is { Count: > 0 } && profileRepositoryWrapper != null;
            friendsConnected.root.SetActive(showFriendsConnected);
            if (showFriendsConnected)
            {
                friendsConnected.amountContainer.SetActive(friends!.Count > friendsConnected.thumbnails.Length);
                friendsConnected.amountLabel.text = $"+{friends.Count - friendsConnected.thumbnails.Length}";

                var friendsThumbnails = friendsConnected.thumbnails;
                for (var i = 0; i < friendsThumbnails.Length; i++)
                {
                    bool friendExists = i < friends.Count;
                    friendsThumbnails[i].root.SetActive(friendExists);
                    if (!friendExists) continue;
                    Profile.CompactInfo friendInfo = friends[i];
                    friendsThumbnails[i].picture.Setup(profileRepositoryWrapper!, friendInfo);
                }
            }

            featuredTag.SetActive(false);
            foreach (string category in placeInfo.categories)
            {
                if (category.Equals(FEATURED_CATEGORY, StringComparison.OrdinalIgnoreCase))
                {
                    featuredTag.SetActive(true);
                    break;
                }
            }

            deleteButton.gameObject.SetActive(userOwnsPlace);

            //Make sure to remove listeners before setting values in order to avoid unwanted calls to previously subscribed methods
            LikeToggleChanged = null;
            DislikeToggleChanged = null;
            FavoriteToggleChanged = null;
            HomeToggleChanged = null;

            SilentlySetLikeToggle(placeInfo.user_like);
            SilentlySetDislikeToggle(placeInfo.user_dislike);
            SilentlySetFavoriteToggle(placeInfo.user_favorite);
            SilentlySetHomeToggle(isHome);
        }

        public void SubscribeToInteractions(Action<PlaceInfo, bool, PlaceCardView> likeToggleChanged,
            Action<PlaceInfo, bool, PlaceCardView> dislikeToggleChanged,
            Action<PlaceInfo, bool, PlaceCardView> favoriteToggleChanged,
            Action<PlaceInfo, bool, PlaceCardView> homeToggleChanged,
            Action<PlaceInfo, Vector2, PlaceCardView> shareButtonClicked,
            Action<PlaceInfo> infoButtonClicked,
            Action<PlaceInfo> jumpInButtonClicked,
            Action<PlaceInfo> deleteButtonClicked,
            Action<PlaceInfo, PlaceCardView> mainButtonClicked)
        {
            LikeToggleChanged = null;
            DislikeToggleChanged = null;
            FavoriteToggleChanged = null;
            HomeToggleChanged = null;
            ShareButtonClicked = null;
            InfoButtonClicked = null;
            JumpInButtonClicked = null;
            DeleteButtonClicked = null;
            MainButtonClicked = null;

            LikeToggleChanged += likeToggleChanged;
            DislikeToggleChanged += dislikeToggleChanged;
            FavoriteToggleChanged += favoriteToggleChanged;
            HomeToggleChanged += homeToggleChanged;
            ShareButtonClicked += shareButtonClicked;
            InfoButtonClicked += infoButtonClicked;
            JumpInButtonClicked += jumpInButtonClicked;
            DeleteButtonClicked += deleteButtonClicked;
            MainButtonClicked += mainButtonClicked;
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

        public void SilentlySetHomeToggle(bool isOn)
        {
            homeToggle.Toggle.SetIsOnWithoutNotify(isOn);
            homeToggle.SetToggleGraphics(isOn);
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
