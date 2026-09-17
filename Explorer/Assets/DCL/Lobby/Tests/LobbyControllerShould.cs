using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Communities;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.Component;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.UI.Buttons;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SystemMenu;
using DCL.UserInAppInitializationFlow;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.Browser;
using DCL.Passport;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyControllerShould
    {
        private const string GENESIS_URL = "https://realm-provider.example.com/main";
        private const string WORLD_SERVER_URL = "https://worlds-content-server.example.com/world";
        private const int RECENT_CARDS = 3;
        private const string OWN_WALLET = "0x0000000000000000000000000000000000000001";
        private const string GENESIS_PLAZA = "Genesis Plaza";
        private const string HOME_WORLD = "myworld.dcl.eth";

        private GameObject root = null!;
        private TMP_Text welcomeText = null!;
        private LobbyHomeCardView homeCard = null!;
        private Button closeButton = null!;
        private Button logoutButton = null!;
        private CharacterPreviewInputDetector avatarInputDetector = null!;
        private CharacterPreviewSettingsSO previewSettings = null!;
        private GameObject recentPlacesSection = null!;
        private LobbyPlaceCardView[] recentPlaceCards = null!;
        private GameObject recommendedPlacesSection = null!;
        private LobbyPlacesCarousel recommendedPlaces = null!;
        private Transform recommendedDots = null!;
        private IInputBlock inputBlock = null!;
        private IMVCManager mvcManager = null!;
        private IPlacesAPIService placesAPIService = null!;
        private IHomePlaceSource homePlace = null!;
        private PlacesData.PlaceInfo genesisPlaza = null!;
        private IRealmNavigator realmNavigator = null!;
        private StartParcel startParcel = null!;
        private LoadingStatus loadingStatus = null!;
        private IWeb3IdentityCache identityCache = null!;
        private IProfileRepository profileRepository = null!;
        private SidebarProfileButtonPresenter profileButtonPresenter = null!;
        private ProfileMenuController profileMenuController = null!;
        private World world = null!;
        private LobbyController controller = null!;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(LobbyControllerShould));
            LobbyView view = root.AddComponent<LobbyView>();

            welcomeText = CreateText(root.transform, "Welcome");
            homeCard = CreateHomeCard(root.transform);

            var closeButtonGo = new GameObject("CloseButton");
            closeButtonGo.transform.SetParent(root.transform);
            closeButton = closeButtonGo.AddComponent<Button>();
            recentPlacesSection = new GameObject("RecentPlaces");
            recentPlacesSection.transform.SetParent(root.transform);
            recentPlaceCards = new LobbyPlaceCardView[RECENT_CARDS];

            for (var i = 0; i < RECENT_CARDS; i++)
                recentPlaceCards[i] = CreatePlaceCard(recentPlacesSection.transform, $"RecentPlace{i}");

            recommendedPlacesSection = new GameObject("RecommendedPlaces");
            recommendedPlacesSection.transform.SetParent(root.transform);
            recommendedPlaces = CreateCarousel(recommendedPlacesSection.transform);

            SetBackingField(view, nameof(LobbyView.WelcomeText), welcomeText);
            SetBackingField(view, nameof(LobbyView.HomeCard), homeCard);
            SetBackingField(view, nameof(LobbyView.CloseButton), closeButton);
            SetBackingField(view, nameof(LobbyView.CharacterPreviewView), CreateCharacterPreviewView());
            SetBackingField(view, nameof(LobbyView.RecentPlacesSection), recentPlacesSection);
            SetBackingField(view, nameof(LobbyView.RecentPlaceCards), recentPlaceCards);
            SetBackingField(view, nameof(LobbyView.ProfileWidgetView), CreateProfileWidgetView());
            SetBackingField(view, nameof(LobbyView.ProfileMenuView), CreateProfileMenuView());
            SetBackingField(view, nameof(LobbyView.ProfileMenuCloserButton), CreateButton(root.transform, "ProfileMenuCloser"));
            SetBackingField(view, nameof(LobbyView.RecommendedPlacesSection), recommendedPlacesSection);
            SetBackingField(view, nameof(LobbyView.RecommendedPlaces), recommendedPlaces);

            inputBlock = Substitute.For<IInputBlock>();
            mvcManager = Substitute.For<IMVCManager>();
            loadingStatus = new LoadingStatus();
            world = World.Create();

            // Without an own profile the avatar preview is never initialized, which keeps the rendering stack out of the test
            ISelfProfile selfProfile = Substitute.For<ISelfProfile>();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<Profile?>(null));

            placesAPIService = Substitute.For<IPlacesAPIService>();
            placesAPIService.GetRecentlyVisitedPlaces().Returns(new List<string>());
            ArrangeRecommendedPlaces();

            // No home by default: the substitute reports neither a parcel nor a world
            homePlace = Substitute.For<IHomePlaceSource>();
            genesisPlaza = CreatePlace(GENESIS_PLAZA, Vector2Int.zero);
            ArrangePlace(Vector2Int.zero, genesisPlaza);

            realmNavigator = Substitute.For<IRealmNavigator>();
            startParcel = new StartParcel(Vector2Int.zero);

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.Genesis).Returns(GENESIS_URL);
            urlsSource.Url(DecentralandUrl.WorldServer).Returns(WORLD_SERVER_URL);

            // Without an identity the widget has nothing to load until a test provides one
            identityCache = Substitute.For<IWeb3IdentityCache>();
            identityCache.Identity.Returns((IWeb3Identity?)null);
            profileRepository = Substitute.For<IProfileRepository>();
            IProfileCache profileCache = Substitute.For<IProfileCache>();
            var profileChangesBus = new ProfileChangesBus();

            profileButtonPresenter = new SidebarProfileButtonPresenter(view.ProfileWidgetView, identityCache, profileRepository, profileChangesBus);

            profileMenuController = new ProfileMenuController(() => view.ProfileMenuView, identityCache, world, default(Entity), new UnityAppWebBrowser(urlsSource),
                Substitute.For<ICompositeWeb3Provider>(), Substitute.For<IUserInAppInitializationFlow>(), profileCache, Substitute.For<IPassportBridge>(),
                new ProfileRepositoryWrapper(profileRepository, profileCache, Substitute.For<ISpriteCache>(), identityCache));

            controller = new LobbyController(() => view, inputBlock, loadingStatus, mvcManager, selfProfile, profileChangesBus,
                Substitute.For<ICharacterPreviewFactory>(), new CharacterPreviewEventBus(), new LobbyAvatarSettings(), world,
                placesAPIService, homePlace, realmNavigator, urlsSource, startParcel, new ThumbnailLoader(Substitute.For<ISpriteCache>()),
                profileButtonPresenter, profileMenuController);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            controller.Dispose();
            profileButtonPresenter.Dispose();
            profileMenuController.Dispose();
            World.Destroy(world);
            Object.DestroyImmediate(previewSettings);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CloseOnJumpInAndToggleInputBlock()
        {
            // Arrange
            UniTask lifeCycle = Launch(isStartup: true);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));
            inputBlock.Received(1).Disable(InputMapComponent.BLOCK_USER_INPUT);

            // Act
            homeCard.JumpInButton.Button.onClick.Invoke();
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Assert
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString(GENESIS_URL)));
            inputBlock.Received(1).Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        [Test]
        public void WelcomeWithoutANameWhenTheProfileIsMissing()
        {
            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(welcomeText.text, Is.EqualTo("WELCOME!"));
        }

        [Test]
        public void ShowGenesisPlazaWhenNoHomeIsSet()
        {
            // Act
            Launch(isStartup: true);

            // Assert
            placesAPIService.Received(1).GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>());
            Assert.That(homeCard.Place, Is.SameAs(genesisPlaza));
            Assert.That(homeCard.TitleText.text, Is.EqualTo(GENESIS_PLAZA));
            Assert.That(homeCard.CreatorText.text, Is.EqualTo("creator"));
            Assert.That(homeCard.JumpInButton.Button.interactable, Is.True);
        }

        [Test]
        public void ShowTheHomeParcelPlace()
        {
            // Arrange
            var homeParcel = new Vector2Int(10, 20);
            PlacesData.PlaceInfo home = CreatePlace("home", homeParcel);
            homePlace.CurrentHomeCoordinates.Returns(homeParcel);
            ArrangePlace(homeParcel, home);

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(homeCard.Place, Is.SameAs(home));
            placesAPIService.DidNotReceive().GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>());
        }

        [Test]
        public void ShowTheHomeWorld()
        {
            // Arrange
            PlacesData.PlaceInfo homeWorld = CreatePlace("my world", Vector2Int.zero, HOME_WORLD);
            homePlace.IsWorldHome.Returns(true);
            homePlace.CurrentHomeWorldName.Returns(HOME_WORLD);
            placesAPIService.GetWorldByNameAsync(HOME_WORLD, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(homeWorld));

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(homeCard.Place, Is.SameAs(homeWorld));
            placesAPIService.DidNotReceiveWithAnyArgs().GetPlaceAsync(default, default);
        }

        [Test]
        public void FallBackToGenesisPlazaWhenTheHomeCannotBeResolved()
        {
            // Arrange: the home parcel no longer hosts a place
            var homeParcel = new Vector2Int(10, 20);
            homePlace.CurrentHomeCoordinates.Returns(homeParcel);
            ArrangePlace(homeParcel, null);

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(homeCard.Place, Is.SameAs(genesisPlaza));
        }

        [Test]
        public void KeepJumpInUsableWhenThePlacesAPIFails()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            placesAPIService.GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>()).Returns(UniTask.FromException<PlacesData.PlaceInfo?>(new Exception("offline")));
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            homeCard.JumpInButton.Button.onClick.Invoke();

            // Assert
            Assert.That(homeCard.TitleText.text, Is.EqualTo(GENESIS_PLAZA));
            Assert.That(homeCard.OnlineCounter.activeSelf, Is.False);
            Assert.That(startParcel.Peek(), Is.EqualTo(Vector2Int.zero));
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString(GENESIS_URL)));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void DisableJumpInWhileTheHomePlaceLoads()
        {
            // Arrange
            var pending = new UniTaskCompletionSource<PlacesData.PlaceInfo?>();
            placesAPIService.GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>()).Returns(pending.Task);
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            homeCard.JumpInButton.Button.onClick.Invoke();

            // Assert
            Assert.That(homeCard.JumpInButton.Button.interactable, Is.False);
            Assert.That(homeCard.Place, Is.Null);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));

            pending.TrySetResult(genesisPlaza);
            Assert.That(homeCard.JumpInButton.Button.interactable, Is.True);
            Assert.That(homeCard.Place, Is.SameAs(genesisPlaza));
        }

        [Test]
        public void CountTheConnectedUsersOverTheUserCount()
        {
            // Arrange
            genesisPlaza.user_count = 7;
            genesisPlaza.connected_addresses = new[] { "0x1", "0x2" };

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(homeCard.OnlineCounter.activeSelf, Is.True);
            Assert.That(homeCard.OnlineCountText.text, Is.EqualTo("2"));
        }

        [Test]
        public void HideTheOnlineCounterWhenNobodyIsThere()
        {
            // Arrange
            genesisPlaza.user_count = 0;

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(homeCard.OnlineCounter.activeSelf, Is.False);
        }

        [Test]
        public void CloseOnCloseButton()
        {
            // Arrange
            UniTask lifeCycle = Launch(isStartup: false);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));

            // Act
            closeButton.onClick.Invoke();

            // Assert
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void StayOpenOnLogout()
        {
            // Arrange
            UniTask lifeCycle = Launch(isStartup: true);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));

            // Act
            logoutButton.onClick.Invoke();

            // Assert
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void RefreshTheProfileWidgetOnShow()
        {
            // Arrange
            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(OWN_WALLET));
            identityCache.Identity.Returns(identity);

            // Act
            Launch(isStartup: true);

            // Assert
            profileRepository.Received(1).GetAsync(Arg.Is<string>(id => string.Equals(id, OWN_WALLET, StringComparison.OrdinalIgnoreCase)), 0, null, Arg.Any<CancellationToken>(),
                true, IProfileRepository.FetchBehaviour.Default, ProfileTier.Kind.Compact, Arg.Any<IPartitionComponent?>());
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ShowCloseButtonOnlyWhenNotStartup(bool isStartup, bool closeButtonVisible)
        {
            // Act
            Launch(isStartup);

            // Assert
            Assert.That(closeButton.gameObject.activeSelf, Is.EqualTo(closeButtonVisible));
        }

        [Test]
        public void OpenTheBackpackWhenTheAvatarIsClicked()
        {
            // Arrange
            Launch(isStartup: true).Forget();

            // Act
            avatarInputDetector.OnPointerClick(new PointerEventData(EventSystem.current));

            // Assert
            mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<ExplorePanelView, ExplorePanelParameter>>(c => c.InputData.Section == ExploreSections.Backpack), Arg.Any<CancellationToken>());
        }

        [TestCase(LoadingStatus.LoadingStage.Init, false)]
        [TestCase(LoadingStatus.LoadingStage.AuthenticationScreenShowing, false)]
        [TestCase(LoadingStatus.LoadingStage.PlayerTeleporting, false)]
        [TestCase(LoadingStatus.LoadingStage.Completed, true)]
        public void BeClosableByEscapeOnlyOnceTheWorldIsLoaded(LoadingStatus.LoadingStage stage, bool expected)
        {
            // Act
            loadingStatus.SetCurrentStage(stage);

            // Assert
            Assert.That(controller.CanBeClosedByEscape, Is.EqualTo(expected));
        }

        private UniTask Launch(bool isStartup) =>
            controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 0), new LobbyParameter(isStartup), CancellationToken.None);
        [Test]
        public void HideRecentPlacesWhenNothingWasVisited()
        {
            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(recentPlacesSection.activeSelf, Is.False);
            placesAPIService.DidNotReceive().GetDestinationsByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>(), Arg.Any<bool>(), Arg.Any<bool?>());
        }

        [Test]
        public void ShowRecentPlacesInVisitOrderRegardlessOfTheResponseOrder()
        {
            // Arrange
            PlacesData.PlaceInfo newest = CreatePlace("newest", new Vector2Int(10, 20));
            PlacesData.PlaceInfo older = CreatePlace("older", new Vector2Int(-5, 5));
            ArrangeRecentPlaces(new List<string> { newest.id, older.id, "deleted" }, older, newest);

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(recentPlacesSection.activeSelf, Is.True);
            Assert.That(recentPlaceCards[0].Place, Is.SameAs(newest));
            Assert.That(recentPlaceCards[0].TitleText.text, Is.EqualTo("newest"));
            Assert.That(recentPlaceCards[1].Place, Is.SameAs(older));
            Assert.That(recentPlaceCards[2].gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RequestTheWholeHistoryAndFillTheCardsWithTheFirstResolvedPlaces()
        {
            // Arrange: the endpoint only knows a subset of the history, so unresolved ids are skipped over
            var history = new List<string> { "unknown", "b", "c", "d", "e" };
            ArrangeRecentPlaces(history, CreatePlace("e", Vector2Int.zero), CreatePlace("d", Vector2Int.zero), CreatePlace("c", Vector2Int.zero), CreatePlace("b", Vector2Int.zero));

            // Act
            Launch(isStartup: true);

            // Assert
            placesAPIService.Received(1).GetDestinationsByIdsAsync(Arg.Is<IEnumerable<string>>(ids => CountOf(ids) == history.Count), Arg.Any<CancellationToken>(), Arg.Any<bool>(), Arg.Any<bool?>());
            Assert.That(recentPlaceCards[0].Place!.id, Is.EqualTo("b"));
            Assert.That(recentPlaceCards[1].Place!.id, Is.EqualTo("c"));
            Assert.That(recentPlaceCards[2].Place!.id, Is.EqualTo("d"));
        }

        [Test]
        public void SetTheStartParcelWhenAGenesisPlaceIsPickedBeforeTheWorldLoads()
        {
            // Arrange
            PlacesData.PlaceInfo place = CreatePlace("plaza", new Vector2Int(10, 20));
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            recentPlaceCards[0].Button.onClick.Invoke();

            // Assert
            Assert.That(startParcel.Peek(), Is.EqualTo(new Vector2Int(10, 20)));
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString(GENESIS_URL)));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
        }

        [Test]
        public void SetTheStartRealmWhenAWorldIsPickedBeforeTheWorldLoads()
        {
            // Arrange
            PlacesData.PlaceInfo place = CreatePlace("my world", Vector2Int.zero, "MyWorld.dcl.eth");
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            recentPlaceCards[0].Button.onClick.Invoke();

            // Assert
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString($"{WORLD_SERVER_URL}/myworld.dcl.eth")));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            realmNavigator.DidNotReceiveWithAnyArgs().TryChangeRealmAsync(default, default);
        }

        [Test]
        public void TeleportWhenAGenesisPlaceIsPickedInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            PlacesData.PlaceInfo place = CreatePlace("plaza", new Vector2Int(10, 20));
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: false);

            // Act
            recentPlaceCards[0].Button.onClick.Invoke();

            // Assert
            realmNavigator.Received(1).TeleportToParcelAsync(new Vector2Int(10, 20), Arg.Any<CancellationToken>(), false);
            Assert.That(startParcel.Realm, Is.Null);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void ChangeRealmWhenAWorldIsPickedInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            PlacesData.PlaceInfo place = CreatePlace("my world", Vector2Int.zero, "myworld.dcl.eth");
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            Launch(isStartup: false);

            // Act
            recentPlaceCards[0].Button.onClick.Invoke();

            // Assert
            realmNavigator.Received(1).TryChangeRealmAsync(URLDomain.FromString($"{WORLD_SERVER_URL}/myworld.dcl.eth"), Arg.Any<CancellationToken>(), default, true, true);
        }

        [Test]
        public void HideRecommendedPlacesWhenNoneAreFeatured()
        {
            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(recommendedPlacesSection.activeSelf, Is.False);
        }

        [Test]
        public void ShowFeaturedPlacesInTheCarouselWithOneDotPerPage()
        {
            // Arrange
            var featured = new PlacesData.PlaceInfo[7];

            for (var i = 0; i < featured.Length; i++)
                featured[i] = CreatePlace($"featured{i}", new Vector2Int(i, 0));

            ArrangeRecommendedPlaces(featured);

            // Act
            Launch(isStartup: true);

            // Assert
            placesAPIService.Received(1).GetHighlightedDestinationsAsync(Arg.Any<CancellationToken>());
            Assert.That(recommendedPlacesSection.activeSelf, Is.True);
            Assert.That(recommendedPlaces.Cards.Count, Is.EqualTo(featured.Length));
            Assert.That(recommendedPlaces.Cards[0].Place, Is.SameAs(featured[0]));
            Assert.That(recommendedPlaces.Cards[6].TitleText.text, Is.EqualTo("featured6"));
            Assert.That(ActiveDots(), Is.EqualTo(3));
            Assert.That(recommendedPlaces.CurrentPage, Is.EqualTo(0));
        }

        [Test]
        public void ReuseCarouselCardsAcrossShows()
        {
            // Arrange
            ArrangeRecommendedPlaces(CreatePlace("a", Vector2Int.zero), CreatePlace("b", Vector2Int.zero), CreatePlace("c", Vector2Int.zero), CreatePlace("d", Vector2Int.zero));
            Launch(isStartup: true);
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Act
            ArrangeRecommendedPlaces(CreatePlace("e", Vector2Int.zero));
            Launch(isStartup: true);

            // Assert
            Assert.That(recommendedPlaces.Cards.Count, Is.EqualTo(4));
            Assert.That(recommendedPlaces.Cards[0].Place!.id, Is.EqualTo("e"));
            Assert.That(recommendedPlaces.Cards[1].gameObject.activeSelf, Is.False);
            Assert.That(ActiveDots(), Is.EqualTo(0));
        }

        [Test]
        public void JumpInWhenAFeaturedPlaceIsPicked()
        {
            // Arrange
            ArrangeRecommendedPlaces(CreatePlace("featured", new Vector2Int(-3, 7)));
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            recommendedPlaces.Cards[0].Button.onClick.Invoke();

            // Assert
            Assert.That(startParcel.Peek(), Is.EqualTo(new Vector2Int(-3, 7)));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        private int ActiveDots()
        {
            var count = 0;

            foreach (Transform dot in recommendedDots)
                if (dot.gameObject.activeSelf)
                    count++;

            return count;
        }

        private void ArrangeRecommendedPlaces(params PlacesData.PlaceInfo[] featured) =>
            placesAPIService.GetHighlightedDestinationsAsync(Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult<PlacesData.IPlacesAPIResponse>(new PlacesData.PlacesAPIResponse { data = new List<PlacesData.PlaceInfo>(featured), total = featured.Length }));

        private void ArrangePlace(Vector2Int parcel, PlacesData.PlaceInfo? place) =>
            placesAPIService.GetPlaceAsync(parcel, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(place));

        private void ArrangeRecentPlaces(List<string> history, params PlacesData.PlaceInfo[] response)
        {
            placesAPIService.GetRecentlyVisitedPlaces().Returns(history);

            placesAPIService.GetDestinationsByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>(), Arg.Any<bool>(), Arg.Any<bool?>())
                            .Returns(UniTask.FromResult<PlacesData.IPlacesAPIResponse>(new PlacesData.PlacesAPIResponse { data = new List<PlacesData.PlaceInfo>(response), total = response.Length }));
        }

        private static PlacesData.PlaceInfo CreatePlace(string title, Vector2Int basePosition, string worldName = "")
        {
            var place = new PlacesData.PlaceInfo(basePosition)
            {
                id = title,
                title = title,
                image = string.Empty,
                contact_name = "creator",
                world_name = worldName,
                base_position_processed = basePosition,
            };

            return place;
        }

        private static int CountOf(IEnumerable<string> ids)
        {
            var count = 0;

            foreach (string _ in ids)
                count++;

            return count;
        }

        private LobbyPlacesCarousel CreateCarousel(Transform parent)
        {
            var carouselGo = new GameObject("Carousel", typeof(RectTransform));
            carouselGo.transform.SetParent(parent);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(carouselGo.transform);
            ((RectTransform)viewportGo.transform).sizeDelta = new Vector2(600f, 150f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform);

            ScrollRect scrollRect = carouselGo.AddComponent<ScrollRect>();
            scrollRect.viewport = (RectTransform)viewportGo.transform;
            scrollRect.content = (RectTransform)contentGo.transform;

            LobbyPlaceCardView cardTemplate = CreatePlaceCard(contentGo.transform, "CardTemplate");
            cardTemplate.gameObject.SetActive(false);

            recommendedDots = new GameObject("Dots", typeof(RectTransform)).transform;
            recommendedDots.SetParent(carouselGo.transform);
            var dotGo = new GameObject("DotTemplate", typeof(RectTransform));
            dotGo.transform.SetParent(recommendedDots);
            Image dotTemplate = dotGo.AddComponent<Image>();
            dotGo.SetActive(false);

            LobbyPlacesCarousel carousel = carouselGo.AddComponent<LobbyPlacesCarousel>();
            SetField(carousel, "scrollRect", scrollRect);
            SetField(carousel, "cardTemplate", cardTemplate);
            SetField(carousel, "dotTemplate", dotTemplate);
            SetField(carousel, "cardsPerPage", RECENT_CARDS);

            return carousel;
        }

        private static LobbyPlaceCardView CreatePlaceCard(Transform parent, string name)
        {
            var cardGo = new GameObject(name);
            cardGo.transform.SetParent(parent);
            LobbyPlaceCardView card = cardGo.AddComponent<LobbyPlaceCardView>();

            SetBackingField(card, nameof(LobbyPlaceCardView.Button), cardGo.AddComponent<Button>());
            SetBackingField(card, nameof(LobbyPlaceCardView.Thumbnail), CreateImageView(cardGo.transform));
            SetBackingField(card, nameof(LobbyPlaceCardView.TitleText), CreateText(cardGo.transform, "Title"));
            SetBackingField(card, nameof(LobbyPlaceCardView.CreatorText), CreateText(cardGo.transform, "Creator"));

            return card;
        }

        private static LobbyHomeCardView CreateHomeCard(Transform parent)
        {
            var cardGo = new GameObject("HomeCard");
            cardGo.transform.SetParent(parent);
            LobbyHomeCardView card = cardGo.AddComponent<LobbyHomeCardView>();

            // ButtonView wires its Button in Awake, so the object stays inactive until the fields are assigned
            var jumpInGo = new GameObject("JumpIn");
            jumpInGo.SetActive(false);
            jumpInGo.transform.SetParent(cardGo.transform);
            ButtonView jumpIn = jumpInGo.AddComponent<ButtonView>();
            SetBackingField(jumpIn, nameof(ButtonView.Button), jumpInGo.AddComponent<Button>());
            SetBackingField(jumpIn, "images", Array.Empty<Image>());
            SetBackingField(jumpIn, "text", CreateText(jumpInGo.transform, "Label"));
            jumpInGo.SetActive(true);

            var counterGo = new GameObject("OnlineCounter");
            counterGo.transform.SetParent(cardGo.transform);

            SetBackingField(card, nameof(LobbyHomeCardView.JumpInButton), jumpIn);
            SetBackingField(card, nameof(LobbyHomeCardView.Thumbnail), CreateImageView(cardGo.transform));
            SetBackingField(card, nameof(LobbyHomeCardView.TitleText), CreateText(cardGo.transform, "Title"));
            SetBackingField(card, nameof(LobbyHomeCardView.CreatorText), CreateText(cardGo.transform, "Creator"));
            SetBackingField(card, nameof(LobbyHomeCardView.OnlineCounter), counterGo);
            SetBackingField(card, nameof(LobbyHomeCardView.OnlineCountText), CreateText(counterGo.transform, "Count"));

            return card;
        }

        private static ImageView CreateImageView(Transform parent)
        {
            var thumbnailGo = new GameObject("Thumbnail");
            thumbnailGo.transform.SetParent(parent);
            Image image = thumbnailGo.AddComponent<Image>();
            ImageView thumbnail = thumbnailGo.AddComponent<ImageView>();
            SetBackingField(thumbnail, nameof(ImageView.Image), image);
            return thumbnail;
        }

        private static TMP_Text CreateText(Transform parent, string name)
        {
            var textGo = new GameObject(name);
            textGo.transform.SetParent(parent);
            return textGo.AddComponent<TextMeshProUGUI>();
        }

        private ProfileWidgetView CreateProfileWidgetView()
        {
            // HoverableButton wires its Button in Awake, so the object stays inactive until the field is assigned
            var widgetGo = new GameObject("ProfileWidget");
            widgetGo.SetActive(false);
            widgetGo.transform.SetParent(root.transform);
            ProfileWidgetView widget = widgetGo.AddComponent<ProfileWidgetView>();

            HoverableButton openProfileButton = widgetGo.AddComponent<HoverableButton>();
            SetBackingField(openProfileButton, nameof(HoverableButton.Button), widgetGo.AddComponent<Button>());

            var pictureGo = new GameObject("ProfilePicture");
            pictureGo.transform.SetParent(widgetGo.transform);
            ProfilePictureView picture = pictureGo.AddComponent<ProfilePictureView>();

            var thumbnailGo = new GameObject("Thumbnail");
            thumbnailGo.transform.SetParent(pictureGo.transform);
            Image image = thumbnailGo.AddComponent<Image>();
            ImageView thumbnail = thumbnailGo.AddComponent<ImageView>();
            SetBackingField(thumbnail, nameof(ImageView.Image), image);
            SetField(picture, "thumbnailImageView", thumbnail);

            SetBackingField(widget, nameof(ProfileWidgetView.ProfilePictureView), picture);
            SetBackingField(widget, nameof(ProfileWidgetView.OpenProfileButton), openProfileButton);
            widgetGo.SetActive(true);

            return widget;
        }

        private ProfileMenuView CreateProfileMenuView()
        {
            var menuGo = new GameObject("ProfileMenu");
            menuGo.transform.SetParent(root.transform);
            ProfileMenuView menu = menuGo.AddComponent<ProfileMenuView>();

            SystemMenuView systemMenu = menuGo.AddComponent<SystemMenuView>();
            logoutButton = CreateButton(menuGo.transform, "Logout");
            SetBackingField(systemMenu, nameof(SystemMenuView.LogoutButton), logoutButton);
            SetBackingField(menu, nameof(ProfileMenuView.SystemMenuView), systemMenu);

            return menu;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent);
            return buttonGo.AddComponent<Button>();
        }

        private CharacterPreviewView CreateCharacterPreviewView()
        {
            var previewGo = new GameObject("CharacterPreviewView");
            previewGo.transform.SetParent(root.transform);
            CharacterPreviewView previewView = previewGo.AddComponent<CharacterPreviewView>();
            avatarInputDetector = previewGo.AddComponent<CharacterPreviewInputDetector>();

            previewSettings = ScriptableObject.CreateInstance<CharacterPreviewSettingsSO>();
            SetBackingField(previewSettings, nameof(CharacterPreviewSettingsSO.cursorSettings), Array.Empty<CharacterPreviewInputCursorSetting>());

            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewInputDetector), avatarInputDetector);
            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewCursorContainer), previewGo.AddComponent<CharacterPreviewCursorContainer>());
            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewSettingsSo), previewSettings);

            return previewView;
        }

        private static void SetBackingField(object target, string propertyName, object value) =>
            SetField(target, $"<{propertyName}>k__BackingField", value);

        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                  .SetValue(target, value);
    }
}
