using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Places
{
    public class PlaceDetailPanelView : ViewBase, IView
    {
        private const int CATEGORY_TAGS_POOL_DEFAULT_CAPACITY = 10;
        private const string NO_DESCRIPTION_TEXT = "No description.";

        [Header("Panel")]
        [SerializeField] private Button backgroundCloseButton = null!;
        [SerializeField] private Button closeButton = null!;
        [SerializeField] private ScrollRect mainScroll = null!;

        [Header("Place info")]
        [SerializeField] private ImageView placeThumbnailImage = null!;
        [SerializeField] private Sprite defaultPlaceThumbnail = null!;
        [SerializeField] private TMP_Text placeNameText = null!;
        [SerializeField] private ProfilePictureView creatorThumbnail = null!;
        [SerializeField] private TMP_Text creatorNameText = null!;
        [SerializeField] private TMP_Text likeRateText = null!;
        [SerializeField] private TMP_Text visitsText = null!;
        [SerializeField] private GameObject liveTag = null!;
        [SerializeField] private TMP_Text onlineMembersText = null!;
        [SerializeField] private FriendsConnectedConfig friendsConnected;
        [SerializeField] private TMP_Text descriptionText = null!;
        [SerializeField] private TMP_Text coordsText = null!;
        [SerializeField] private TMP_Text parcelsText = null!;
        [SerializeField] private TMP_Text favoritesText = null!;
        [SerializeField] private TMP_Text updatedDateText = null!;

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

        [Header("Categories")]
        [SerializeField] private GameObject categoriesModule = null!;
        [SerializeField] private Transform categoriesContainer = null!;
        [SerializeField] private PlaceCategoryButton categoryButtonPrefab = null!;
        [SerializeField] private PlaceCategoriesSO placesCategoriesConfiguration = null!;

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

        private IObjectPool<PlaceCategoryButton> categoryButtonsPool = null!;
        private readonly List<KeyValuePair<string, PlaceCategoryButton>> currentCategories = new ();

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

            // Categories pool configuration
            categoryButtonsPool = new ObjectPool<PlaceCategoryButton>(
                InstantiateCategoryButtonPrefab,
                defaultCapacity: CATEGORY_TAGS_POOL_DEFAULT_CAPACITY,
                actionOnGet: categoryButtonView =>
                {
                    categoryButtonView.gameObject.SetActive(true);
                    categoryButtonView.transform.SetAsLastSibling();
                },
                actionOnRelease: categoryButtonView => categoryButtonView.gameObject.SetActive(false));

            mainScroll.SetScrollSensitivityBasedOnPlatform();
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
            CancellationToken cancellationToken,
            List<Profile.CompactInfo>? friends = null,
            ProfileRepositoryWrapper? profileRepositoryWrapper = null)
        {
            currentPlaceInfo = placeInfo;
            mainScroll.verticalNormalizedPosition = 1;

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(placeInfo.image, placeThumbnailImage, defaultPlaceThumbnail, cancellationToken, true).Forget();
            placeNameText.text = placeInfo.title;
            creatorThumbnail.gameObject.SetActive(false);
            creatorNameText.text = !string.IsNullOrEmpty(placeInfo.contact_name) ? placeInfo.contact_name : "Unknown";
            likeRateText.text = $"{(placeInfo.like_rate_as_float ?? 0) * 100:F0}%";
            visitsText.text = GetKFormat(placeInfo.user_visits);
            likeToggle.Toggle.isOn = placeInfo.user_like;
            dislikeToggle.Toggle.isOn = placeInfo.user_dislike;
            favoriteToggle.Toggle.isOn = placeInfo.user_favorite;
            onlineMembersText.text = $"{(placeInfo.connected_addresses != null ? placeInfo.connected_addresses.Length : placeInfo.user_count)}";
            descriptionText.text = !string.IsNullOrEmpty(placeInfo.description) ? placeInfo.description : NO_DESCRIPTION_TEXT;
            coordsText.text = placeInfo.base_position;
            parcelsText.text = placeInfo.Positions.Length.ToString();
            favoritesText.text = GetKFormat(placeInfo.favorites);
            updatedDateText.text = !string.IsNullOrEmpty(placeInfo.updated_at) ? DateTimeOffset.Parse(placeInfo.updated_at).ToString("dd/MM/yyyy") : "-";
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

            bool isWorld = !string.IsNullOrEmpty(placeInfo.world_name);
            startExitNavigationButtonsContainer.SetActive(!isWorld);
            if (!isWorld)
                SetNavigation(isNavigating);

            SetCategories(placeInfo.categories);
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

        private PlaceCategoryButton InstantiateCategoryButtonPrefab()
        {
            PlaceCategoryButton invitedCommunityCardView = Instantiate(categoryButtonPrefab, categoriesContainer);
            return invitedCommunityCardView;
        }

        private void SetCategories(string[] categories)
        {
            ClearCategories();

            var categoriesFound = 0;
            foreach (string categoryId in categories)
            {
                foreach (var categoryData in placesCategoriesConfiguration.categories)
                {
                    if (categoryId != categoryData.id)
                        continue;

                    CreateAndSetupCategoryButton(categoryData);
                    categoriesFound++;
                }
            }

            categoriesModule.SetActive(categoriesFound > 0);
        }

        private void CreateAndSetupCategoryButton(PlaceCategoriesSO.PlaceCategoryData categoryData)
        {
            PlaceCategoryButton categoryButtonView = categoryButtonsPool.Get();
            categoryButtonView.Configure(categoryData);
            currentCategories.Add(new KeyValuePair<string, PlaceCategoryButton>(categoryData.id, categoryButtonView));
        }

        private void ClearCategories()
        {
            foreach (var categoryButton in currentCategories)
                categoryButtonsPool.Release(categoryButton.Value);

            currentCategories.Clear();
        }
    }
}
