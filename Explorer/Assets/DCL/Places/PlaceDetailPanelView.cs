using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Places
{
    public class PlaceDetailPanelView : ViewBase, IView
    {
        [Header("Panel")]
        [SerializeField] private Button backgroundCloseButton = null!;
        [SerializeField] private Button closeButton = null!;

        [Header("Place info")]
        [SerializeField] private ImageView placeThumbnailImage = null!;
        [SerializeField] private Sprite defaultPlaceThumbnail = null!;
        [SerializeField] private TMP_Text placeNameText = null!;
        [SerializeField] private ProfilePictureView creatorThumbnail = null!;
        [SerializeField] private TMP_Text creatorNameText = null!;
        [SerializeField] private TMP_Text likeRateText = null!;
        [SerializeField] private TMP_Text visitsText = null!;

        [Header("Buttons")]
        [SerializeField] private ToggleView likeToggle = null!;
        [SerializeField] private ToggleView dislikeToggle = null!;
        [SerializeField] private ToggleView favoriteToggle = null!;
        [SerializeField] private Button shareButton = null!;
        [SerializeField] private Button jumpInButton = null!;
        [SerializeField] private GameObject startExitNavigationButtonsContainer = null!;
        [SerializeField] private Button startNavigationButton = null!;
        [SerializeField] private Button exitNavigationButton = null!;

        [Header("Configuration")]
        [SerializeField] private PlacePlaceCardContextMenuConfiguration placeCardContextMenuConfiguration = null!;

        public event Action<PlacesData.PlaceInfo, bool>? LikeToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool>? DislikeToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool>? FavoriteToggleChanged;
        public event Action<PlacesData.PlaceInfo>? ShareButtonClicked;
        public event Action<PlacesData.PlaceInfo>? CopyLinkButtonClicked;
        public event Action<PlacesData.PlaceInfo>? JumpInButtonClicked;
        public event Action<PlacesData.PlaceInfo>? StartNavigationButtonClicked;
        public event Action? ExitNavigationButtonClicked;

        private readonly UniTask[] closeTasks = new UniTask[2];

        private PlacesData.PlaceInfo currentPlaceInfo = null!;
        private GenericContextMenu? contextMenu;

        private CancellationToken ct;
        private CancellationTokenSource? openContextMenuCts;

        private void Awake()
        {
            likeToggle.Toggle.onValueChanged.AddListener(value => LikeToggleChanged?.Invoke(currentPlaceInfo, value));
            dislikeToggle.Toggle.onValueChanged.AddListener(value => DislikeToggleChanged?.Invoke(currentPlaceInfo, value));
            favoriteToggle.Toggle.onValueChanged.AddListener(value => FavoriteToggleChanged?.Invoke(currentPlaceInfo, value));
            shareButton.onClick.AddListener(() => OpenCardContextMenu(shareButton.transform.position));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(currentPlaceInfo));
            startNavigationButton.onClick.AddListener(() => StartNavigationButtonClicked?.Invoke(currentPlaceInfo));
            exitNavigationButton.onClick.AddListener(() => ExitNavigationButtonClicked?.Invoke());

            contextMenu = new GenericContextMenu(placeCardContextMenuConfiguration.ContextMenuWidth, verticalLayoutPadding: placeCardContextMenuConfiguration.VerticalPadding, elementsSpacing: placeCardContextMenuConfiguration.ElementsSpacing)
                         .AddControl(new ButtonContextMenuControlSettings(placeCardContextMenuConfiguration.ShareText, placeCardContextMenuConfiguration.ShareSprite, () => ShareButtonClicked?.Invoke(currentPlaceInfo)))
                         .AddControl(new ButtonContextMenuControlSettings(placeCardContextMenuConfiguration.CopyLinkText, placeCardContextMenuConfiguration.CopyLinkSprite, () => CopyLinkButtonClicked?.Invoke(currentPlaceInfo)));
        }

        public UniTask[] GetCloseTasks()
        {
            closeTasks[0] = backgroundCloseButton.OnClickAsync(ct);
            closeTasks[1] = closeButton.OnClickAsync(ct);
            return closeTasks;
        }

        public void ConfigurePlaceData(
            PlacesData.PlaceInfo placeInfo,
            bool isNavigating,
            ThumbnailLoader thumbnailLoader,
            CancellationToken cancellationToken)
        {
            currentPlaceInfo = placeInfo;
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(placeInfo.image, placeThumbnailImage, defaultPlaceThumbnail, cancellationToken, true).Forget();
            placeNameText.text = placeInfo.title;
            creatorThumbnail.gameObject.SetActive(false);
            creatorNameText.text = !string.IsNullOrEmpty(placeInfo.contact_name) ? placeInfo.contact_name : "Unknown";
            likeRateText.text = $"{(placeInfo.like_rate_as_float ?? 0) * 100:F0}%";
            visitsText.text = GetKFormat(placeInfo.user_visits);
            likeToggle.Toggle.isOn = placeInfo.user_like;
            dislikeToggle.Toggle.isOn = placeInfo.user_dislike;
            favoriteToggle.Toggle.isOn = placeInfo.user_favorite;

            bool isWorld = !string.IsNullOrEmpty(placeInfo.world_name);
            startExitNavigationButtonsContainer.SetActive(!isWorld);
            if (!isWorld)
                SetNavigation(isNavigating);
        }

        public void SetCreatorThumbnail(
            ProfileRepositoryWrapper profileRepositoryWrapper,
            Profile.CompactInfo? creatorProfile)
        {
            creatorThumbnail.gameObject.SetActive(creatorProfile != null);
            if (creatorProfile != null)
                creatorThumbnail.Setup(profileRepositoryWrapper, creatorProfile.Value);
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

        public void SetNavigation(bool isNavigating)
        {
            startNavigationButton.gameObject.SetActive(!isNavigating);
            exitNavigationButton.gameObject.SetActive(isNavigating);
        }

        private static string GetKFormat(int num)
        {
            if (num < 1000)
                return num.ToString();

            float divided = num / 1000.0f;
            divided = (int)(divided * 100) / 100f;
            return $"{divided:F2}k";
        }

        private void OpenCardContextMenu(Vector2 position)
        {
            openContextMenuCts = openContextMenuCts.SafeRestart();
            ViewDependencies.ContextMenuOpener.OpenContextMenu(
                new GenericContextMenuParameter(contextMenu!, position), openContextMenuCts.Token);
        }
    }
}
