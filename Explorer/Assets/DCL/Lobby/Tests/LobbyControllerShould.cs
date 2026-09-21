using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Clipboard;
using DCL.Communities;
using DCL.Communities.EventInfo;
using DCL.Events;
using DCL.EventsApi;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.Component;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.UI.Buttons;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using MVC;
using NSubstitute;
using NSubstitute.Core;
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
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyControllerShould
    {
        private const string GENESIS_URL = "https://realm-provider.example.com/main";
        private const string WORLD_SERVER_URL = "https://worlds-content-server.example.com/world";
        private const string EVENTS_API_URL = "https://events.example.com/api/events";
        private const int RECENT_CARDS = 3;
        private const string OWN_WALLET = "0x0000000000000000000000000000000000000001";
        private const string GENESIS_PLAZA = "Genesis Plaza";
        private const string START_WORLD = "myworld.dcl.eth";

        private GameObject root = null!;
        private LobbyView view = null!;
        private ISelfProfile selfProfile = null!;
        private IDecentralandUrlsSource urlsSource = null!;
        private HttpEventsApiService eventsApiService = null!;
        private ProfileChangesBus profileChangesBus = null!;
        private TMP_Text welcomeText = null!;
        private LobbyLandingCardView landingCard = null!;
        private Button closeButton = null!;
        private Button notificationsButton = null!;
        private Button openProfileButton = null!;
        private CharacterPreviewInputDetector avatarInputDetector = null!;
        private CharacterPreviewSettingsSO previewSettings = null!;
        private GameObject recentPlacesSection = null!;
        private LobbyPlaceCardView[] recentPlaceCards = null!;
        private GameObject recommendedPlacesSection = null!;
        private LobbyCarouselView recommendedPlaces = null!;
        private Transform recommendedDots = null!;
        private GameObject eventsSection = null!;
        private GameObject liveEventsSection = null!;
        private LobbyCarouselView liveEvents = null!;
        private GameObject upcomingEventsSection = null!;
        private LobbyEventRailView upcomingEvents = null!;
        private IWebRequestController webRequestController = null!;
        private IInputBlock inputBlock = null!;
        private IMVCManager mvcManager = null!;
        private IPlacesAPIService placesAPIService = null!;
        private IRealmData realmData = null!;
        private IHomePlaceSource homePlace = null!;
        private PlacesData.PlaceInfo genesisPlaza = null!;
        private IRealmNavigator realmNavigator = null!;
        private ISystemClipboard clipboard = null!;
        private StartParcel startParcel = null!;
        private LoadingStatus loadingStatus = null!;
        private IWeb3IdentityCache identityCache = null!;
        private IProfileRepository profileRepository = null!;
        private SidebarProfileButtonPresenter profileButtonPresenter = null!;
        private World world = null!;
        private LobbyController controller = null!;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(LobbyControllerShould));
            view = root.AddComponent<LobbyView>();

            welcomeText = CreateText(root.transform, "Welcome");
            landingCard = CreateLandingCard(root.transform);

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
            recommendedPlaces = CreateCarousel(recommendedPlacesSection.transform, CreatePlaceCard, RECENT_CARDS, out recommendedDots);

            eventsSection = new GameObject("Events");
            eventsSection.transform.SetParent(root.transform);
            liveEventsSection = new GameObject("LiveEvents");
            liveEventsSection.transform.SetParent(eventsSection.transform);
            liveEvents = CreateCarousel(liveEventsSection.transform, CreateLiveEventCard, 1, out _);
            upcomingEventsSection = new GameObject("UpcomingEvents");
            upcomingEventsSection.transform.SetParent(eventsSection.transform);
            upcomingEvents = CreateEventRail(upcomingEventsSection.transform);

            SetBackingField(view, nameof(LobbyView.WelcomeText), welcomeText);
            SetBackingField(view, nameof(LobbyView.LandingCard), landingCard);
            SetBackingField(view, nameof(LobbyView.CloseButton), closeButton);
            SetBackingField(view, nameof(LobbyView.CharacterPreviewView), CreateCharacterPreviewView());
            SetBackingField(view, nameof(LobbyView.RecentPlacesSection), recentPlacesSection);
            SetBackingField(view, nameof(LobbyView.RecentPlaceCards), recentPlaceCards);
            SetBackingField(view, nameof(LobbyView.ProfileWidgetView), CreateProfileWidgetView());
            SetBackingField(view, nameof(LobbyView.ProfileMenuView), CreateProfileMenuView());
            notificationsButton = CreateButton(root.transform, "Notifications");
            SetBackingField(view, nameof(LobbyView.NotificationsButton), notificationsButton);
            SetBackingField(view, nameof(LobbyView.NotificationsMenuView), CreateNotificationsMenuView());
            SetBackingField(view, nameof(LobbyView.RecommendedPlacesSection), recommendedPlacesSection);
            SetBackingField(view, nameof(LobbyView.RecommendedPlaces), recommendedPlaces);
            SetBackingField(view, nameof(LobbyView.EventsSection), eventsSection);
            SetBackingField(view, nameof(LobbyView.LiveEventsSection), liveEventsSection);
            SetBackingField(view, nameof(LobbyView.LiveEvents), liveEvents);
            SetBackingField(view, nameof(LobbyView.UpcomingEventsSection), upcomingEventsSection);
            SetBackingField(view, nameof(LobbyView.UpcomingEvents), upcomingEvents);

            inputBlock = Substitute.For<IInputBlock>();
            mvcManager = Substitute.For<IMVCManager>();
            loadingStatus = new LoadingStatus();
            world = World.Create();

            // Without an own profile the avatar preview is never initialized, which keeps the rendering stack out of the test
            selfProfile = Substitute.For<ISelfProfile>();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<Profile?>(null));

            placesAPIService = Substitute.For<IPlacesAPIService>();
            placesAPIService.GetRecentlyVisitedPlaces().Returns(new List<string>());
            ArrangeRecommendedPlaces();

            // Genesis realm landing on parcel 0,0 by default, so the landing card shows Genesis Plaza
            realmData = Substitute.For<IRealmData>();
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.GenesisCity));
            homePlace = Substitute.For<IHomePlaceSource>();
            genesisPlaza = CreatePlace(GENESIS_PLAZA, Vector2Int.zero);
            ArrangePlace(Vector2Int.zero, genesisPlaza);

            realmNavigator = Substitute.For<IRealmNavigator>();
            clipboard = Substitute.For<ISystemClipboard>();
            startParcel = new StartParcel(Vector2Int.zero);

            urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.Genesis).Returns(GENESIS_URL);
            urlsSource.Url(DecentralandUrl.WorldServer).Returns(WORLD_SERVER_URL);
            urlsSource.Url(DecentralandUrl.ApiEvents).Returns(EVENTS_API_URL);
            urlsSource.GetOriginalUrl(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());

            // The events service has no interface: the requests it issues are mocked instead
            webRequestController = Substitute.For<IWebRequestController>();
            eventsApiService = new HttpEventsApiService(webRequestController, urlsSource);
            ArrangeEvents();

            // Without an identity the widget has nothing to load until a test provides one
            identityCache = Substitute.For<IWeb3IdentityCache>();
            identityCache.Identity.Returns((IWeb3Identity?)null);
            profileRepository = Substitute.For<IProfileRepository>();
            profileChangesBus = new ProfileChangesBus();

            profileButtonPresenter = new SidebarProfileButtonPresenter(view.ProfileWidgetView, identityCache, profileRepository, profileChangesBus);

            CreateController();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            controller.Dispose();
            profileButtonPresenter.Dispose();
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
            landingCard.JumpInButton.Button.onClick.Invoke();
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Assert
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(startParcel.Realm, Is.Null);
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
        public void ShowGenesisPlazaWhenLandingAtTheOrigin()
        {
            // Act
            Launch(isStartup: true);

            // Assert
            placesAPIService.Received(1).GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>());
            Assert.That(landingCard.TitleText.text, Is.EqualTo(GENESIS_PLAZA));
            Assert.That(landingCard.CreatorText.text, Is.EqualTo("creator"));
            Assert.That(landingCard.JumpInButton.Button.interactable, Is.True);
        }

        [Test]
        public void ShowThePlaceAtTheStartParcel()
        {
            // Arrange: the launch settings already resolved the landing parcel (app argument, home or spawn flag)
            var landing = new Vector2Int(10, 20);
            startParcel.Assign(landing);
            ArrangePlace(landing, CreatePlace("landing", landing));

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo("landing"));
            placesAPIService.DidNotReceive().GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>());
        }

        [Test]
        public void ShowTheWorldTheSessionStartsIn()
        {
            // Arrange
            ArrangeWorldRealm();
            placesAPIService.GetWorldByNameAsync(START_WORLD, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(CreatePlace("my world", Vector2Int.zero, START_WORLD)));

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo("my world"));
            placesAPIService.DidNotReceiveWithAnyArgs().GetPlaceAsync(default, default);
        }

        [Test]
        public void KeepTheStartupDestinationForTheWholeSession()
        {
            // Arrange: the session started in Genesis at 0,0; later the user is in a world and set a home
            Launch(isStartup: true);
            controller.HideViewAsync(CancellationToken.None).Forget();
            ArrangeWorldRealm();
            homePlace.CurrentHomeCoordinates.Returns(new Vector2Int(10, 20));
            placesAPIService.ClearReceivedCalls();

            // Act
            Launch(isStartup: false);

            // Assert
            placesAPIService.Received(1).GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>());
            placesAPIService.DidNotReceiveWithAnyArgs().GetWorldByNameAsync(default!, default);
            Assert.That(landingCard.TitleText.text, Is.EqualTo(GENESIS_PLAZA));
        }

        [Test]
        public void FollowTheHomeWhenTheSessionStartedAtHome()
        {
            // Arrange: home won the launch chain, then the user moved it during the session
            var home = new Vector2Int(10, 20);
            RestartWithStartParcel(new StartParcel(Vector2Int.zero, source: StartParcelSource.Home));
            homePlace.CurrentHomeCoordinates.Returns(home);
            ArrangePlace(home, CreatePlace("home", home));

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo("home"));
            placesAPIService.DidNotReceive().GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>());
        }

        [Test]
        public void FollowTheHomeWorldWhenTheSessionStartedAtHome()
        {
            // Arrange
            RestartWithStartParcel(new StartParcel(Vector2Int.zero, source: StartParcelSource.Home));
            homePlace.IsWorldHome.Returns(true);
            homePlace.CurrentHomeWorldName.Returns(START_WORLD);
            placesAPIService.GetWorldByNameAsync(START_WORLD, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(CreatePlace("home world", Vector2Int.zero, START_WORLD)));

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo("home world"));
        }

        [Test]
        public void FallBackToTheStartupDestinationWhenTheHomeIsUnset()
        {
            // Arrange: home won the launch chain but was cleared before the lobby reopened
            RestartWithStartParcel(new StartParcel(Vector2Int.zero, source: StartParcelSource.Home));

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo(GENESIS_PLAZA));
        }

        [Test]
        public void DescribeTheDestinationOfflineWhenNoPlaceIsRegisteredThere()
        {
            // Arrange
            var landing = new Vector2Int(10, 20);
            startParcel.Assign(landing);
            ArrangePlace(landing, null);

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo("10,20"));
            Assert.That(landingCard.JumpInButton.Button.interactable, Is.True);
        }

        [Test]
        public void TeleportToTheLandingParcelWhenJumpingInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            UniTask lifeCycle = Launch(isStartup: false);

            // Act
            landingCard.JumpInButton.Button.onClick.Invoke();

            // Assert
            realmNavigator.Received(1).TeleportToParcelAsync(Vector2Int.zero, Arg.Any<CancellationToken>(), false);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void ChangeRealmToTheLandingWorldWhenJumpingInWorld()
        {
            // Arrange: the worlds endpoint knows nothing about it, so the offline stand-in still carries the world name
            ArrangeWorldRealm();
            placesAPIService.GetWorldByNameAsync(START_WORLD, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(null));
            startParcel.ConsumeByTeleportOperation();
            Launch(isStartup: false);

            // Act
            landingCard.JumpInButton.Button.onClick.Invoke();

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo(START_WORLD));
            realmNavigator.Received(1).TryChangeRealmAsync(URLDomain.FromString($"{WORLD_SERVER_URL}/{START_WORLD}"), Arg.Any<CancellationToken>(), default, true, true);
        }

        [Test]
        public void KeepJumpInUsableWhenThePlacesAPIFails()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            placesAPIService.GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>()).Returns(UniTask.FromException<PlacesData.PlaceInfo?>(new Exception("offline")));
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            landingCard.JumpInButton.Button.onClick.Invoke();

            // Assert
            Assert.That(landingCard.TitleText.text, Is.EqualTo(GENESIS_PLAZA));
            Assert.That(landingCard.OnlineCountText.text, Is.EqualTo("0"));
            Assert.That(startParcel.Peek(), Is.EqualTo(Vector2Int.zero));
            Assert.That(startParcel.Realm, Is.Null);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void DisableJumpInWhileTheLandingPlaceLoads()
        {
            // Arrange
            var pending = new UniTaskCompletionSource<PlacesData.PlaceInfo?>();
            placesAPIService.GetPlaceAsync(Vector2Int.zero, Arg.Any<CancellationToken>()).Returns(pending.Task);
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            landingCard.JumpInButton.Button.onClick.Invoke();

            // Assert
            Assert.That(landingCard.JumpInButton.Button.interactable, Is.False);
            Assert.That(landingCard.TitleText.text, Is.Empty);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));

            pending.TrySetResult(genesisPlaza);
            Assert.That(landingCard.JumpInButton.Button.interactable, Is.True);
            Assert.That(landingCard.TitleText.text, Is.EqualTo(GENESIS_PLAZA));
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
            Assert.That(landingCard.OnlineCounter.activeSelf, Is.True);
            Assert.That(landingCard.OnlineCountText.text, Is.EqualTo("2"));
        }

        [Test]
        public void ShowAZeroOnlineCountWhenNobodyIsThere()
        {
            // Arrange
            genesisPlaza.user_count = 0;

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(landingCard.OnlineCounter.activeSelf, Is.True);
            Assert.That(landingCard.OnlineCountText.text, Is.EqualTo("0"));
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
        public void OpenTheProfileMenuAsALobbyPopup()
        {
            // Arrange
            Launch(isStartup: true).Forget();

            // Act
            openProfileButton.onClick.Invoke();

            // Assert
            mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<ProfileMenuView, LobbyPopupParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void OpenTheNotificationsAsALobbyPopup()
        {
            // Arrange
            Launch(isStartup: true).Forget();

            // Act
            notificationsButton.onClick.Invoke();

            // Assert
            mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<NotificationsMenuView, LobbyPopupParameter>>(), Arg.Any<CancellationToken>());
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

        [Test]
        public void ShowTheLobbyAgainWhenThePanelThatReplacedItClosesAtStartup()
        {
            // Arrange: the Explore panel took the screen over, which hides the lobby without closing it
            Launch(isStartup: true).Forget();
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Act
            CloseAnotherView();

            // Assert
            mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<LobbyView, LobbyParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void LeaveTheLobbyClosedOnceTheUserJumpedIn()
        {
            // Arrange
            Launch(isStartup: true).Forget();
            landingCard.JumpInButton.Button.onClick.Invoke();
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Act
            CloseAnotherView();

            // Assert
            mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<LobbyView, LobbyParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void LeaveTheLobbyClosedInWorld()
        {
            // Arrange
            Launch(isStartup: false).Forget();
            controller.HideViewAsync(CancellationToken.None).Forget();

            // Act
            CloseAnotherView();

            // Assert
            mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<LobbyView, LobbyParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void LeaveTheLobbyClosedWhenTheStartupFlowWasTakenOver()
        {
            // Arrange: a Logout replaced the lobby with the authentication screen
            using var takenOver = new CancellationTokenSource();
            Launch(isStartup: true, startupToken: takenOver.Token).Forget();
            controller.HideViewAsync(CancellationToken.None).Forget();
            takenOver.Cancel();

            // Act
            CloseAnotherView();

            // Assert
            mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<LobbyView, LobbyParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void ReleaseTheStartupFlowOnlyWhenTheUserJumpsIn()
        {
            // Arrange
            var jumpedIn = 0;
            Launch(isStartup: true, jumpedIn: () => jumpedIn++).Forget();

            // Act: opening the backpack takes the lobby off the screen without releasing the flow
            avatarInputDetector.OnPointerClick(new PointerEventData(EventSystem.current));

            // Assert
            Assert.That(jumpedIn, Is.Zero);

            // Act
            landingCard.JumpInButton.Button.onClick.Invoke();

            // Assert
            Assert.That(jumpedIn, Is.EqualTo(1));
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

        private void CreateController()
        {
            controller = new LobbyController(() => view, inputBlock, loadingStatus, mvcManager, selfProfile, profileChangesBus,
                Substitute.For<ICharacterPreviewFactory>(), new CharacterPreviewEventBus(), new LobbyAvatarSettings(), new GameObject(nameof(LobbyStage)).AddComponent<LobbyStage>(), world,
                placesAPIService, realmData, homePlace, eventsApiService, new EventCardActionsController(eventsApiService, new UnityAppWebBrowser(urlsSource), realmNavigator, clipboard, urlsSource),
                realmNavigator, urlsSource, startParcel, new ThumbnailLoader(Substitute.For<ISpriteCache>()),
                profileButtonPresenter, friends: null);
        }

        private void RestartWithStartParcel(StartParcel parcel)
        {
            controller.Dispose();
            startParcel = parcel;
            CreateController();
        }

        private UniTask Launch(bool isStartup, Action? jumpedIn = null, CancellationToken startupToken = default) =>
            controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Fullscreen, 0), new LobbyParameter(isStartup, jumpedIn, startupToken), CancellationToken.None);

        /// <summary>
        ///     Reports the closure of the view that took the screen over, the way the MVC manager does.
        /// </summary>
        private void CloseAnotherView() =>
            mvcManager.OnViewClosed += Raise.Event<Action<IController>>(Substitute.For<IController>());

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
            Assert.That(recentPlaceCards[0].TitleText.text, Is.EqualTo("newest"));
            Assert.That(recentPlaceCards[1].TitleText.text, Is.EqualTo("older"));
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
            Assert.That(recentPlaceCards[0].TitleText.text, Is.EqualTo("b"));
            Assert.That(recentPlaceCards[1].TitleText.text, Is.EqualTo("c"));
            Assert.That(recentPlaceCards[2].TitleText.text, Is.EqualTo("d"));
        }

        [Test]
        public void OpenThePlaceDetailsInsteadOfJumpingInWhenAPlaceCardIsClicked()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            PlacesData.PlaceInfo place = CreatePlace("plaza", new Vector2Int(10, 20));
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: false);

            // Act
            recentPlaceCards[0].Button.onClick.Invoke();

            // Assert
            mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<PlaceDetailPanelView, PlaceDetailPanelParameter>>(c => c.InputData.PlaceData.title == "plaza"), Arg.Any<CancellationToken>());
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void SetTheStartParcelWhenJumpingInAGenesisPlaceFromThePlaceDetailsBeforeTheWorldLoads()
        {
            // Arrange
            PlacesData.PlaceInfo place = CreatePlace("plaza", new Vector2Int(10, 20));
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: true);
            recentPlaceCards[0].Button.onClick.Invoke();
            PlaceDetailPanelParameter details = ShownPlaceDetails();

            // Act
            details.JumpInHandler!(details.PlaceData);

            // Assert
            Assert.That(startParcel.Peek(), Is.EqualTo(new Vector2Int(10, 20)));
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString(GENESIS_URL)));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
        }

        [Test]
        public void SetTheStartRealmWhenJumpingInAWorldFromThePlaceDetailsBeforeTheWorldLoads()
        {
            // Arrange
            PlacesData.PlaceInfo place = CreatePlace("my world", Vector2Int.zero, "MyWorld.dcl.eth");
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: true);
            recentPlaceCards[0].Button.onClick.Invoke();
            PlaceDetailPanelParameter details = ShownPlaceDetails();

            // Act
            details.JumpInHandler!(details.PlaceData);

            // Assert
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString($"{WORLD_SERVER_URL}/myworld.dcl.eth")));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            realmNavigator.DidNotReceiveWithAnyArgs().TryChangeRealmAsync(default, default);
        }

        [Test]
        public void TeleportWhenJumpingInAGenesisPlaceFromThePlaceDetailsInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            PlacesData.PlaceInfo place = CreatePlace("plaza", new Vector2Int(10, 20));
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: false);
            recentPlaceCards[0].Button.onClick.Invoke();
            PlaceDetailPanelParameter details = ShownPlaceDetails();

            // Act
            details.JumpInHandler!(details.PlaceData);

            // Assert
            realmNavigator.Received(1).TeleportToParcelAsync(new Vector2Int(10, 20), Arg.Any<CancellationToken>(), false);
            Assert.That(startParcel.Realm, Is.Null);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void ChangeRealmWhenJumpingInAWorldFromThePlaceDetailsInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            PlacesData.PlaceInfo place = CreatePlace("my world", Vector2Int.zero, "myworld.dcl.eth");
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            Launch(isStartup: false);
            recentPlaceCards[0].Button.onClick.Invoke();
            PlaceDetailPanelParameter details = ShownPlaceDetails();

            // Act
            details.JumpInHandler!(details.PlaceData);

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
            Assert.That(recommendedPlaces.Cards[0].TitleText.text, Is.EqualTo("featured0"));
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
            Assert.That(recommendedPlaces.Cards[0].TitleText.text, Is.EqualTo("e"));
            Assert.That(recommendedPlaces.Cards[1].gameObject.activeSelf, Is.False);
            Assert.That(ActiveDots(), Is.EqualTo(0));
        }

        [Test]
        public void JumpInWhenAFeaturedPlaceIsPickedFromThePlaceDetails()
        {
            // Arrange
            ArrangeRecommendedPlaces(CreatePlace("featured", new Vector2Int(-3, 7)));
            UniTask lifeCycle = Launch(isStartup: true);
            recommendedPlaces.Cards[0].Button.onClick.Invoke();
            PlaceDetailPanelParameter details = ShownPlaceDetails();

            // Act
            details.JumpInHandler!(details.PlaceData);

            // Assert
            Assert.That(startParcel.Peek(), Is.EqualTo(new Vector2Int(-3, 7)));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void SetTheStartParcelWhenJumpingInFromARecentCardBeforeTheWorldLoads()
        {
            // Arrange
            PlacesData.PlaceInfo place = CreatePlace("plaza", new Vector2Int(10, 20));
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: true);

            // Act
            recentPlaceCards[0].JumpInButton!.Button.onClick.Invoke();

            // Assert
            Assert.That(startParcel.Peek(), Is.EqualTo(new Vector2Int(10, 20)));
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString(GENESIS_URL)));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<PlaceDetailPanelView, PlaceDetailPanelParameter>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void TeleportWhenJumpingInFromARecentCardInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            PlacesData.PlaceInfo place = CreatePlace("plaza", new Vector2Int(10, 20));
            ArrangeRecentPlaces(new List<string> { place.id }, place);
            UniTask lifeCycle = Launch(isStartup: false);

            // Act
            recentPlaceCards[0].JumpInButton!.Button.onClick.Invoke();

            // Assert
            realmNavigator.Received(1).TeleportToParcelAsync(new Vector2Int(10, 20), Arg.Any<CancellationToken>(), false);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void ChangeRealmWhenJumpingInFromAFeaturedCardInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            ArrangeRecommendedPlaces(CreatePlace("my world", Vector2Int.zero, "myworld.dcl.eth"));
            Launch(isStartup: false);

            // Act
            recommendedPlaces.Cards[0].JumpInButton!.Button.onClick.Invoke();

            // Assert
            realmNavigator.Received(1).TryChangeRealmAsync(URLDomain.FromString($"{WORLD_SERVER_URL}/myworld.dcl.eth"), Arg.Any<CancellationToken>(), default, true, true);
        }

        [Test]
        public void ShowTheOnlineCountOnEveryPlaceCard()
        {
            // Arrange
            PlacesData.PlaceInfo crowded = CreatePlace("crowded", Vector2Int.zero);
            crowded.user_count = 7;
            crowded.connected_addresses = new[] { "0x1", "0x2" };
            PlacesData.PlaceInfo empty = CreatePlace("empty", Vector2Int.one);
            ArrangeRecentPlaces(new List<string> { crowded.id, empty.id }, crowded, empty);

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(recentPlaceCards[0].OnlineCounter.activeSelf, Is.True);
            Assert.That(recentPlaceCards[0].OnlineCountText.text, Is.EqualTo("2"));
            Assert.That(recentPlaceCards[1].OnlineCounter.activeSelf, Is.True);
            Assert.That(recentPlaceCards[1].OnlineCountText.text, Is.EqualTo("0"));
        }

        [Test]
        public void HideEventsWhenNothingIsScheduled()
        {
            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(eventsSection.activeSelf, Is.False);
        }

        [Test]
        public void ShowLiveEventsAheadOfTheUpcomingOnesSortedByStartTime()
        {
            // Arrange
            EventDTO concert = CreateEvent("concert", new Vector2Int(1, 2), TimeSpan.FromMinutes(-30), live: true, connectedUsers: 24);
            EventDTO soon = CreateEvent("soon", new Vector2Int(3, 4), TimeSpan.FromHours(2));
            EventDTO later = CreateEvent("later", new Vector2Int(5, 6), TimeSpan.FromDays(3));
            ArrangeEvents(concert, later, soon);

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(EventRequestsReceived(), Is.EqualTo(1));
            Assert.That(eventsSection.activeSelf, Is.True);
            Assert.That(liveEventsSection.activeSelf, Is.True);
            Assert.That(liveEvents.Cards.Count, Is.EqualTo(1));
            Assert.That(LiveEventCard(0).TitleText.text, Is.EqualTo("concert"));
            Assert.That(LiveEventCard(0).HostText.text, Is.EqualTo("By host"));
            Assert.That(LiveEventCard(0).AttendeesGroup.activeSelf, Is.True);
            Assert.That(LiveEventCard(0).AttendeesText.text, Is.EqualTo("24"));

            Assert.That(upcomingEventsSection.activeSelf, Is.True);
            Assert.That(upcomingEvents.Cards.Count, Is.EqualTo(2));
            Assert.That(UpcomingText(0, "eventText"), Is.EqualTo("soon"));
            Assert.That(UpcomingText(0, "eventDate"), Is.EqualTo("In 2 hours"));
            Assert.That(UpcomingText(0, "hostName"), Is.EqualTo("By <b>host</b>"));
            Assert.That(UpcomingText(1, "eventText"), Is.EqualTo("later"));
            Assert.That(UpcomingText(1, "eventDate"), Is.EqualTo("In 3 days"));
        }

        [Test]
        public void HideTheLiveCarouselWhenNothingIsLive()
        {
            // Arrange
            ArrangeEvents(CreateEvent("soon", Vector2Int.zero, TimeSpan.FromMinutes(20)));

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(eventsSection.activeSelf, Is.True);
            Assert.That(liveEventsSection.activeSelf, Is.False);
            Assert.That(upcomingEventsSection.activeSelf, Is.True);
            Assert.That(UpcomingText(0, "eventDate"), Is.EqualTo("In 20 min"));
        }

        [Test]
        public void KeepOnlyTheNextUpcomingEventsAndSkipTheLiveOnes()
        {
            // Arrange: the schedule also lists the live event, which belongs to the other carousel
            var scheduled = new EventDTO[12];

            for (var i = 0; i < scheduled.Length; i++)
                scheduled[i] = CreateEvent($"event{i}", Vector2Int.zero, TimeSpan.FromHours(i + 1), live: i == 0);

            ArrangeEvents(scheduled);

            // Act
            Launch(isStartup: true);

            // Assert
            Assert.That(upcomingEvents.Cards.Count, Is.EqualTo(10));
            Assert.That(UpcomingText(0, "eventText"), Is.EqualTo("event1"));
            Assert.That(UpcomingText(9, "eventText"), Is.EqualTo("event10"));
        }

        [Test]
        public void OpenTheEventDetailsInsteadOfJumpingInWhenAnEventCardIsClicked()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            ArrangeEvents(CreateEvent("concert", new Vector2Int(3, 4), TimeSpan.FromMinutes(-5), live: true));
            UniTask lifeCycle = Launch(isStartup: false);

            // Act
            LiveEventCard(0).Button.onClick.Invoke();

            // Assert
            mvcManager.Received(1).ShowAsync(Arg.Is<ShowCommand<EventDetailPanelView, EventDetailPanelParameter>>(c => c.InputData.EventData.Id == "concert"), Arg.Any<CancellationToken>());
            realmNavigator.DidNotReceiveWithAnyArgs().TeleportToParcelAsync(default, default, default);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void HandTheUpcomingCardOverToTheEventDetailsSoTheyStayInSync()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            ArrangeEvents(CreateEvent("party", Vector2Int.zero, TimeSpan.FromHours(1)));
            Launch(isStartup: false);

            // Act
            UpcomingMainButton(0).onClick.Invoke();

            // Assert
            EventDetailPanelParameter details = ShownEventDetails();
            Assert.That(details.EventData.Id, Is.EqualTo("party"));
            Assert.That(details.SummonerEventCard, Is.SameAs(upcomingEvents.Cards[0]));
        }

        [Test]
        public void TeleportToTheEventParcelWhenJumpingInFromTheEventDetailsInWorld()
        {
            // Arrange
            startParcel.ConsumeByTeleportOperation();
            ArrangeEvents(CreateEvent("concert", new Vector2Int(3, 4), TimeSpan.FromMinutes(-5), live: true));
            UniTask lifeCycle = Launch(isStartup: false);
            LiveEventCard(0).Button.onClick.Invoke();
            EventDetailPanelParameter details = ShownEventDetails();

            // Act
            details.JumpInHandler!(details.EventData);

            // Assert
            realmNavigator.Received(1).TeleportToParcelAsync(new Vector2Int(3, 4), Arg.Any<CancellationToken>(), false, true);
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void SetTheStartRealmWhenJumpingInAWorldEventFromTheEventDetailsBeforeTheWorldLoads()
        {
            // Arrange
            ArrangeEvents(CreateEvent("party", Vector2Int.zero, TimeSpan.FromHours(1), server: "MyWorld.dcl.eth"));
            UniTask lifeCycle = Launch(isStartup: true);
            UpcomingMainButton(0).onClick.Invoke();
            EventDetailPanelParameter details = ShownEventDetails();

            // Act
            details.JumpInHandler!(details.EventData);

            // Assert
            Assert.That(startParcel.Realm, Is.EqualTo(URLDomain.FromString($"{WORLD_SERVER_URL}/myworld.dcl.eth")));
            Assert.That(lifeCycle.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            realmNavigator.DidNotReceiveWithAnyArgs().TryChangeRealmAsync(default, default);
        }

        private PlaceDetailPanelParameter ShownPlaceDetails()
        {
            foreach (ICall call in mvcManager.ReceivedCalls())
                if (call.GetArguments()[0] is ShowCommand<PlaceDetailPanelView, PlaceDetailPanelParameter> command)
                    return command.InputData;

            throw new AssertionException("The place details were not shown");
        }

        private EventDetailPanelParameter ShownEventDetails()
        {
            foreach (ICall call in mvcManager.ReceivedCalls())
                if (call.GetArguments()[0] is ShowCommand<EventDetailPanelView, EventDetailPanelParameter> command)
                    return command.InputData;

            throw new AssertionException("The event details were not shown");
        }

        private int ActiveDots()
        {
            var count = 0;

            foreach (Transform dot in recommendedDots)
                if (dot.gameObject.activeSelf)
                    count++;

            return count;
        }

        /// <summary>
        ///     The whole schedule, live events included, answers the single request the lobby issues.
        /// </summary>
        private void ArrangeEvents(params EventDTO[] events) =>
            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<EventDTOListResponse, GenericGetRequest>, EventDTOListResponse>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<EventDTOListResponse, GenericGetRequest>>())
               .Returns(UniTask.FromResult(new EventDTOListResponse { ok = true, data = events }));

        private int EventRequestsReceived()
        {
            var count = 0;

            foreach (ICall call in webRequestController.ReceivedCalls())
                if (call.GetArguments()[0] is RequestEnvelope<GenericGetRequest, GenericGetArguments> envelope && envelope.CommonArguments.URL.Value.StartsWith(EVENTS_API_URL))
                    count++;

            return count;
        }

        private static EventDTO CreateEvent(string id, Vector2Int parcel, TimeSpan startsIn, bool live = false, string server = "", int connectedUsers = 0) =>
            new ()
            {
                id = id,
                name = id,
                image = string.Empty,
                user_name = "host",
                live = live,
                world = server.Length > 0,
                server = server,
                x = parcel.x,
                y = parcel.y,
                connected_addresses = new string[connectedUsers],
                NextStartAtProcessed = DateTime.UtcNow + startsIn,
            };

        private void ArrangeRecommendedPlaces(params PlacesData.PlaceInfo[] featured) =>
            placesAPIService.GetHighlightedDestinationsAsync(Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult<PlacesData.IPlacesAPIResponse>(new PlacesData.PlacesAPIResponse { data = new List<PlacesData.PlaceInfo>(featured), total = featured.Length }));

        private void ArrangeWorldRealm()
        {
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.World));
            realmData.RealmName.Returns(START_WORLD);
        }

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

        private static LobbyCarouselView CreateCarousel(Transform parent, Func<Transform, string, LobbyCardView> createCard, int cardsPerPage, out Transform dots)
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

            LobbyCardView cardTemplate = createCard(contentGo.transform, "CardTemplate");
            cardTemplate.gameObject.SetActive(false);

            dots = new GameObject("Dots", typeof(RectTransform)).transform;
            dots.SetParent(carouselGo.transform);
            var dotGo = new GameObject("DotTemplate", typeof(RectTransform));
            dotGo.transform.SetParent(dots);
            Image dotTemplate = dotGo.AddComponent<Image>();
            dotGo.SetActive(false);
            LobbyCarouselDotsView dotsView = dots.gameObject.AddComponent<LobbyCarouselDotsView>();
            SetField(dotsView, "dotTemplate", dotTemplate);

            LobbyCarouselView carousel = carouselGo.AddComponent<LobbyCarouselView>();
            SetField(carousel, "scrollRect", scrollRect);
            SetField(carousel, "cardTemplate", cardTemplate);
            SetField(carousel, "dots", dotsView);
            SetField(carousel, "cardsPerPage", cardsPerPage);

            return carousel;
        }

        private LobbyLiveEventCardView LiveEventCard(int index) =>
            (LobbyLiveEventCardView)liveEvents.Cards[index];

        private string UpcomingText(int index, string fieldName) =>
            GetField<TMP_Text>(upcomingEvents.Cards[index], fieldName).text;

        private Button UpcomingMainButton(int index) =>
            GetField<Button>(upcomingEvents.Cards[index], "mainButton");

        private static LobbyEventRailView CreateEventRail(Transform parent)
        {
            var railGo = new GameObject("Rail", typeof(RectTransform));
            railGo.transform.SetParent(parent);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(railGo.transform);
            ((RectTransform)viewportGo.transform).sizeDelta = new Vector2(352f, 135f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform);

            ScrollRect scrollRect = railGo.AddComponent<ScrollRect>();
            scrollRect.viewport = (RectTransform)viewportGo.transform;
            scrollRect.content = (RectTransform)contentGo.transform;

            EventCardView cardTemplate = CreateUpcomingEventCard(contentGo.transform, "CardTemplate");
            cardTemplate.gameObject.SetActive(false);

            var dotsGo = new GameObject("Dots", typeof(RectTransform));
            dotsGo.transform.SetParent(railGo.transform);
            var dotGo = new GameObject("DotTemplate", typeof(RectTransform));
            dotGo.transform.SetParent(dotsGo.transform);
            Image dotTemplate = dotGo.AddComponent<Image>();
            dotGo.SetActive(false);
            LobbyCarouselDotsView dotsView = dotsGo.AddComponent<LobbyCarouselDotsView>();
            SetField(dotsView, "dotTemplate", dotTemplate);

            LobbyEventRailView rail = railGo.AddComponent<LobbyEventRailView>();
            SetField(rail, "scrollRect", scrollRect);
            SetField(rail, "cardTemplate", cardTemplate);
            SetField(rail, "dots", dotsView);
            SetField(rail, "cardsPerPage", 1);

            return rail;
        }

        /// <summary>
        ///     The Explore panel's small event card, reduced to what the lobby drives: the card measures its containers in Awake,
        ///     so the object stays inactive until the fields are assigned.
        /// </summary>
        private static EventCardView CreateUpcomingEventCard(Transform parent, string name)
        {
            var cardGo = new GameObject(name, typeof(RectTransform));
            cardGo.SetActive(false);
            cardGo.transform.SetParent(parent);
            EventCardSmallView card = cardGo.AddComponent<EventCardSmallView>();

            TMP_Text host = CreateText(cardGo.transform, "Host");
            var actionsGo = new GameObject("Actions", typeof(RectTransform), typeof(CanvasGroup));
            actionsGo.transform.SetParent(cardGo.transform);

            SetField(card, "mainButton", cardGo.AddComponent<Button>());
            SetField(card, "eventThumbnail", CreateImageView(cardGo.transform));
            SetField(card, "eventText", CreateText(cardGo.transform, "Name"));
            SetField(card, "hostName", host);
            SetField(card, "eventDate", CreateText(cardGo.transform, "Date"));
            SetField(card, "liveMarks", new List<GameObject>());
            SetField(card, "hostCanvasGroup", host.gameObject.AddComponent<CanvasGroup>());
            SetField(card, "actionButtonsCanvasGroup", actionsGo.GetComponent<CanvasGroup>());
            SetField(card, "nameContainer", CreateRect(cardGo.transform, "NameContainer"));
            SetField(card, "dateContainer", CreateRect(cardGo.transform, "DateContainer"));
            cardGo.SetActive(true);

            return card;
        }

        private static LobbyPlaceCardView CreatePlaceCard(Transform parent, string name)
        {
            // The card measures its header and footer in Awake, so the object stays inactive until the fields are assigned
            var cardGo = new GameObject(name);
            cardGo.SetActive(false);
            cardGo.transform.SetParent(parent);
            LobbyPlaceCardView card = cardGo.AddComponent<LobbyPlaceCardView>();

            var jumpInGo = new GameObject("JumpIn");
            jumpInGo.transform.SetParent(cardGo.transform);
            ButtonView jumpIn = jumpInGo.AddComponent<ButtonView>();
            SetBackingField(jumpIn, nameof(ButtonView.Button), jumpInGo.AddComponent<Button>());
            SetBackingField(jumpIn, "images", Array.Empty<Image>());
            SetBackingField(jumpIn, "text", CreateText(jumpInGo.transform, "Label"));

            var counterGo = new GameObject("OnlineCounter");
            counterGo.transform.SetParent(cardGo.transform);
            TMP_Text creator = CreateText(cardGo.transform, "Creator");
            var jumpInGroupGo = new GameObject("JumpInGroup", typeof(RectTransform), typeof(CanvasGroup));
            jumpInGroupGo.transform.SetParent(cardGo.transform);

            SetBackingField(card, nameof(LobbyPlaceCardView.Button), cardGo.AddComponent<Button>());
            SetField(card, "jumpInButton", jumpIn);
            SetBackingField(card, nameof(LobbyPlaceCardView.Thumbnail), CreateImageView(cardGo.transform));
            SetBackingField(card, nameof(LobbyPlaceCardView.TitleText), CreateText(cardGo.transform, "Title"));
            SetBackingField(card, nameof(LobbyPlaceCardView.CreatorText), creator);
            SetBackingField(card, nameof(LobbyPlaceCardView.OnlineCounter), counterGo);
            SetBackingField(card, nameof(LobbyPlaceCardView.OnlineCountText), CreateText(counterGo.transform, "Count"));
            SetField(card, "header", CreateRect(cardGo.transform, "Header"));
            SetField(card, "footer", CreateRect(cardGo.transform, "Footer"));
            SetField(card, "creatorGroup", creator.gameObject.AddComponent<CanvasGroup>());
            SetField(card, "jumpInGroup", jumpInGroupGo.GetComponent<CanvasGroup>());
            cardGo.SetActive(true);

            return card;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var rectGo = new GameObject(name, typeof(RectTransform));
            rectGo.transform.SetParent(parent);
            return (RectTransform)rectGo.transform;
        }

        private static LobbyLiveEventCardView CreateLiveEventCard(Transform parent, string name)
        {
            var cardGo = new GameObject(name);
            cardGo.transform.SetParent(parent);
            LobbyLiveEventCardView card = cardGo.AddComponent<LobbyLiveEventCardView>();

            var attendees = new GameObject("Attendees");
            attendees.transform.SetParent(cardGo.transform);

            SetBackingField(card, nameof(LobbyLiveEventCardView.Button), cardGo.AddComponent<Button>());
            SetBackingField(card, nameof(LobbyLiveEventCardView.Thumbnail), CreateImageView(cardGo.transform));
            SetBackingField(card, nameof(LobbyLiveEventCardView.TitleText), CreateText(cardGo.transform, "Title"));
            SetBackingField(card, nameof(LobbyLiveEventCardView.HostText), CreateText(cardGo.transform, "Host"));
            SetBackingField(card, nameof(LobbyLiveEventCardView.AttendeesGroup), attendees);
            SetBackingField(card, nameof(LobbyLiveEventCardView.AttendeesText), CreateText(attendees.transform, "Count"));

            return card;
        }

        private static LobbyLandingCardView CreateLandingCard(Transform parent)
        {
            var cardGo = new GameObject("LandingCard");
            cardGo.transform.SetParent(parent);
            LobbyLandingCardView card = cardGo.AddComponent<LobbyLandingCardView>();

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

            SetBackingField(card, nameof(LobbyLandingCardView.JumpInButton), jumpIn);
            SetBackingField(card, nameof(LobbyLandingCardView.Thumbnail), CreateImageView(cardGo.transform));
            SetBackingField(card, nameof(LobbyLandingCardView.TitleText), CreateText(cardGo.transform, "Title"));
            SetBackingField(card, nameof(LobbyLandingCardView.CreatorText), CreateText(cardGo.transform, "Creator"));
            SetBackingField(card, nameof(LobbyLandingCardView.OnlineCounter), counterGo);
            SetBackingField(card, nameof(LobbyLandingCardView.OnlineCountText), CreateText(counterGo.transform, "Count"));

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

            HoverableButton hoverableButton = widgetGo.AddComponent<HoverableButton>();
            openProfileButton = widgetGo.AddComponent<Button>();
            SetBackingField(hoverableButton, nameof(HoverableButton.Button), openProfileButton);

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
            SetBackingField(widget, nameof(ProfileWidgetView.OpenProfileButton), hoverableButton);
            widgetGo.SetActive(true);

            return widget;
        }

        private ProfileMenuView CreateProfileMenuView()
        {
            var menuGo = new GameObject("ProfileMenu");
            menuGo.transform.SetParent(root.transform);
            return menuGo.AddComponent<ProfileMenuView>();
        }

        // Kept inactive like the prefab instance: the view wires its buttons on Awake, which never runs here
        private NotificationsMenuView CreateNotificationsMenuView()
        {
            var menuGo = new GameObject("NotificationsMenu");
            menuGo.transform.SetParent(root.transform);
            menuGo.SetActive(false);
            NotificationsMenuView menu = menuGo.AddComponent<NotificationsMenuView>();
            SetBackingField(menu, "foundationCommunityButton", CreateButton(menuGo.transform, "FoundationCommunity"));

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

        private static T GetField<T>(object target, string fieldName)
        {
            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (field != null)
                    return (T)field.GetValue(target);
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            // Private fields are only reachable through the type that declares them, so base classes are walked explicitly
            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }
    }
}
