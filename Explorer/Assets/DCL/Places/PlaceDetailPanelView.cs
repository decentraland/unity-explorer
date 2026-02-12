using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.PlacesAPIService;
using DCL.PrivateWorlds;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.ProfileElements;
using DCL.UI.Utilities;
using DCL.Utilities;
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
        [SerializeField] private ToggleView homeToggle = null!;
        [SerializeField] private Button shareButton = null!;
        [SerializeField] private Button jumpInButton = null!;
        [SerializeField] private GameObject startExitNavigationButtonsContainer = null!;
        [SerializeField] private Button startNavigationButton = null!;
        [SerializeField] private Button exitNavigationButton = null!;

        [Header("World Access")]
        [SerializeField] private GameObject? worldAccessStatusContainer;
        [SerializeField] private TMP_Text? worldAccessStatusText;
        [SerializeField] private Button enterPasswordButton = null!;

        private static readonly Color WORLD_ACCESS_STATUS_RESTRICTED = new (0.761f, 0.722f, 0.792f, 1f);
        private static readonly Color WORLD_ACCESS_STATUS_GREEN = new (0.2f, 0.8f, 0.2f);
        private const string PADDLOCK_CLOSED_SPRITE = "<sprite name=\"PaddlockClosed\">";
        private const string PADDLOCK_OPENED_SPRITE = "<sprite name=\"PaddlockOpened\">";

        [Header("Configuration")]
        [SerializeField] private PlaceContextMenuConfiguration placeCardContextMenuConfiguration = null!;

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
        private readonly List<ReactiveProperty<ProfileThumbnailViewModel>> friendsProfileThumbnails = new ();
        private readonly ReactiveProperty<ProfileThumbnailViewModel> creatorProfileThumbnail = new (ProfileThumbnailViewModel.Default());

        public event Action<PlacesData.PlaceInfo, bool>? LikeToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool>? DislikeToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool>? FavoriteToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool>? HomeToggleChanged;
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
            homeToggle.Toggle.onValueChanged.AddListener(value => HomeToggleChanged?.Invoke(currentPlaceInfo, value));
            shareButton.onClick.AddListener(() => OpenCardContextMenu(shareButton.transform.position));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(currentPlaceInfo));
            enterPasswordButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(currentPlaceInfo));
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

            for (var i = 0; i < friendsConnected.thumbnails.Length; i++)
            {
                friendsProfileThumbnails.Add(new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default()));
                friendsConnected.thumbnails[i].picture.Bind(friendsProfileThumbnails[i]);
            }

            creatorThumbnail.Bind(creatorProfileThumbnail);
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
            HomePlaceEventBus? homePlaceEventBus = null)
        {
            currentPlaceInfo = placeInfo;
            mainScroll.verticalNormalizedPosition = 1;

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(placeInfo.image, placeThumbnailImage, defaultPlaceThumbnail, cancellationToken, true).Forget();
            placeNameText.text = placeInfo.title;
            creatorThumbnail.gameObject.SetActive(false);
            creatorNameText.text = !string.IsNullOrEmpty(placeInfo.contact_name) ? placeInfo.contact_name : "Unknown";
            likeRateText.text = $"{(placeInfo.like_rate_as_float ?? 0) * 100:F0}%";
            visitsText.text = UIUtils.NumberToCompactString(placeInfo.user_visits);
            SilentlySetLikeToggle(placeInfo.user_like);
            SilentlySetDislikeToggle(placeInfo.user_dislike);
            SilentlySetFavoriteToggle(placeInfo.user_favorite);
            onlineMembersText.text = $"{(placeInfo.connected_addresses != null ? placeInfo.connected_addresses.Length : placeInfo.user_count)}";
            descriptionText.text = !string.IsNullOrEmpty(placeInfo.description) ? placeInfo.description : NO_DESCRIPTION_TEXT;
            coordsText.text = placeInfo.base_position;
            parcelsText.text = placeInfo.Positions.Length.ToString();
            favoritesText.text = UIUtils.NumberToCompactString(placeInfo.favorites);
            updatedDateText.text = !string.IsNullOrEmpty(placeInfo.updated_at) ? DateTimeOffset.Parse(placeInfo.updated_at).ToString("dd/MM/yyyy") : "-";
            liveTag.SetActive(placeInfo.live);

            bool isHome = homePlaceEventBus?.IsHome(placeInfo) ?? false;
            SilentlySetHomeToggle(isHome);

            LoadFriendsThumbnailsAsync(friends, cancellationToken).Forget();

            bool isWorld = !string.IsNullOrEmpty(placeInfo.world_name);
            startExitNavigationButtonsContainer.SetActive(!isWorld);
            if (!isWorld)
                SetNavigation(isNavigating);

            if (isWorld)
                HideWorldAccessButtons();
            else
                SetWorldAccessState(WorldAccessCheckResult.Allowed);

            SetCategories(placeInfo.categories);
        }

        private async UniTask LoadFriendsThumbnailsAsync(List<Profile.CompactInfo>? friendProfiles, CancellationToken cancellationToken)
        {
            bool showFriendsConnected = friendProfiles is { Count: > 0 };
            friendsConnected.root.SetActive(showFriendsConnected);

            if (showFriendsConnected)
            {
                friendsConnected.amountContainer.SetActive(friendProfiles!.Count > friendsConnected.thumbnails.Length);
                friendsConnected.amountLabel.text = $"+{friendProfiles.Count - friendsConnected.thumbnails.Length}";

                for (var i = 0; i < friendsConnected.thumbnails.Length; i++)
                {
                    friendsConnected.thumbnails[i].root.SetActive(false);
                    bool friendExists = i < friendProfiles.Count;
                    if (!friendExists) continue;
                    Profile.CompactInfo friendInfo = friendProfiles[i];

                    friendsProfileThumbnails[i].SetLoading(friendInfo.UserNameColor);
                    if (!string.IsNullOrEmpty(friendInfo.FaceSnapshotUrl))
                    {
                        await GetProfileThumbnailCommand.Instance.ExecuteAsync(
                            friendsProfileThumbnails[i],
                            null,
                            friendInfo.UserId,
                            friendInfo.FaceSnapshotUrl,
                            cancellationToken);

                        friendsConnected.thumbnails[i].root.SetActive(friendsProfileThumbnails[i].Value.Sprite != null);
                    }
                }
            }
        }

        public async UniTask SetCreatorThumbnailAsync(Profile.CompactInfo? creatorProfile, CancellationToken cancellationToken)
        {
            creatorThumbnail.gameObject.SetActive(false);

            if (creatorProfile != null)
            {
                creatorProfileThumbnail.SetLoading(creatorProfile.Value.UserNameColor);
                if (!string.IsNullOrEmpty(creatorProfile.Value.FaceSnapshotUrl))
                {
                    await GetProfileThumbnailCommand.Instance.ExecuteAsync(
                        creatorProfileThumbnail,
                        null,
                        creatorProfile.Value.UserId,
                        creatorProfile.Value.FaceSnapshotUrl,
                        cancellationToken);

                    creatorThumbnail.gameObject.SetActive(creatorProfileThumbnail.Value.Sprite != null);
                }
            }
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

        public void SetNavigation(bool isNavigating)
        {
            startNavigationButton.gameObject.SetActive(!isNavigating);
            exitNavigationButton.gameObject.SetActive(isNavigating);
        }

        public void HideWorldAccessButtons()
        {
            jumpInButton.gameObject.SetActive(false);
            enterPasswordButton.gameObject.SetActive(false);
            if (worldAccessStatusText != null)
                worldAccessStatusText.gameObject.SetActive(false);
        }

        public void SetWorldAccessState(WorldAccessCheckResult accessState, WorldAccessType? accessType = null)
        {
            bool isInvited = accessState == WorldAccessCheckResult.Allowed && accessType == WorldAccessType.AllowList;
            bool isRestricted = accessState == WorldAccessCheckResult.AccessDenied || accessState == WorldAccessCheckResult.PasswordRequired;

            if (worldAccessStatusText != null)
            {
                if (isRestricted || isInvited)
                {
                    worldAccessStatusText.gameObject.SetActive(true);
                    if (accessState == WorldAccessCheckResult.AccessDenied)
                    {
                        worldAccessStatusText.text = PADDLOCK_CLOSED_SPRITE + " INVITE ONLY";
                        worldAccessStatusText.color = WORLD_ACCESS_STATUS_RESTRICTED;
                    }
                    else if (accessState == WorldAccessCheckResult.PasswordRequired)
                    {
                        worldAccessStatusText.text = PADDLOCK_CLOSED_SPRITE + " PASSWORD REQUIRED";
                        worldAccessStatusText.color = WORLD_ACCESS_STATUS_RESTRICTED;
                    }
                    else
                    {
                        worldAccessStatusText.text = PADDLOCK_OPENED_SPRITE + " YOU ARE INVITED";
                        worldAccessStatusText.color = WORLD_ACCESS_STATUS_GREEN;
                    }
                }
                else
                    worldAccessStatusText.gameObject.SetActive(false);
            }

            jumpInButton.gameObject.SetActive(accessState is WorldAccessCheckResult.Allowed or WorldAccessCheckResult.CheckFailed);
            enterPasswordButton.gameObject.SetActive(accessState == WorldAccessCheckResult.PasswordRequired);
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
