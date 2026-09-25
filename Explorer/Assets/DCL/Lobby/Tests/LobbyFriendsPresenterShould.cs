using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyFriendsPresenterShould
    {
        // The tracker debounces status changes for 2000ms; the margin absorbs editor loop jitter
        private const int DEBOUNCE_WAIT_MS = 2500;
        private const string AMY_ID = "0x0000000000000000000000000000000000000a11";
        private const string ZED_ID = "0x0000000000000000000000000000000000000b22";
        private const string LOCATING = "Locating…";
        private const string LOBBY = "Lobby";

        private static readonly Vector2Int PARCEL = new (5, 7);

        private GameObject root = null!;
        private DefaultFriendsEventBus eventBus = null!;
        private FriendsConnectivityStatusTracker tracker = null!;
        private IOnlineUsersProvider onlineUsers = null!;
        private IPlacesAPIService places = null!;
        private IPassportBridge passport = null!;
        private LobbyFriendsSectionView section = null!;
        private LoopListView2 loopList = null!;
        private Transform dots = null!;
        private CancellationTokenSource cts = null!;
        private LobbyFriendsPresenter presenter = null!;
        private ClonedCardAwakener cardAwakener = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // The editor domain may still hold an initialized registry from a play-mode session
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistry();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            // The editor domain may still hold a command initialized by a play-mode session
            GetProfileThumbnailCommand.Reset();

            GetProfileThumbnailCommand.Initialize(new GetProfileThumbnailCommand(new ProfileRepositoryWrapper(Substitute.For<IProfileRepository>(),
                Substitute.For<IProfileCache>(), Substitute.For<ISpriteCache>(), Substitute.For<IWeb3IdentityCache>())));

            cardAwakener = new ClonedCardAwakener();
            eventBus = new DefaultFriendsEventBus();
            tracker = new FriendsConnectivityStatusTracker(eventBus, isConnectivityStatusEnabled: true);
            onlineUsers = Substitute.For<IOnlineUsersProvider>();
            SetOnlineUsers();
            places = Substitute.For<IPlacesAPIService>();
            places.GetPlaceAsync(Arg.Any<Vector2Int>(), Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(null));
            places.GetWorldAsync(Arg.Any<Vector2Int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(null));
            passport = Substitute.For<IPassportBridge>();
            passport.ShowAsync(Arg.Any<string>()).Returns(UniTask.CompletedTask);

            root = new GameObject("Root", typeof(RectTransform));
            section = CreateSection(root.transform);
            cts = new CancellationTokenSource();

            presenter = new LobbyFriendsPresenter(section, tracker, onlineUsers, places, passport);
        }

        [TearDown]
        public void TearDown()
        {
            GetProfileThumbnailCommand.Reset();
            presenter.Dispose();
            cts.Cancel();
            cts.Dispose();
            tracker.Dispose();
            Object.DestroyImmediate(root);
        }

        [Test]
        public void HideSectionWithoutOnlineFriends()
        {
            //Act
            presenter.Show(cts.Token);

            //Assert
            Assert.IsFalse(section.gameObject.activeSelf);
            Assert.AreEqual("0 Online", section.OnlineCountText.text);
        }

        [Test]
        public async Task ShowOnlineFriendsSortedByNameAndReadLobbyWhenTheyHaveNoPosition()
        {
            //Arrange
            eventBus.BroadcastFriendConnected(Friend(ZED_ID, "Zed"));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Act
            presenter.Show(cts.Token);
            LobbyFriendCardView card = ShownCard(0);
            string initialLocation = card.LocationText.text;
            await UniTask.DelayFrame(3);

            //Assert
            Assert.IsTrue(section.gameObject.activeSelf);
            Assert.AreEqual("2 Online", section.OnlineCountText.text);
            Assert.AreEqual(AMY_ID, card.UserId);
            Assert.AreEqual(LOCATING, initialLocation);
            Assert.AreEqual(LOBBY, card.LocationText.text);
            card.Hover.OnPointerEnter(new PointerEventData(null));
            Assert.IsFalse(card.JoinButton.gameObject.activeSelf);
            Assert.IsTrue(card.LocationGroup.activeSelf);
        }

        [Test]
        public async Task ShowPlaceTitleAndJoinWithTheLivePosition()
        {
            //Arrange
            OnlineUserData location = InGenesis(AMY_ID, PARCEL);
            SetOnlineUsers(location);
            places.GetPlaceAsync(PARCEL, Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(CreatePlace("Cool Place", PARCEL)));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            OnlineUserData? joined = null;
            presenter.JoinRequested = data => joined = data;

            //Act
            presenter.Show(cts.Token);
            LobbyFriendCardView card = ShownCard(0);
            await UniTask.DelayFrame(3);
            card.Hover.OnPointerEnter(new PointerEventData(null));
            bool joinShownOnHover = card.JoinButton.gameObject.activeSelf;
            card.JoinButton.onClick.Invoke();
            await UniTask.DelayFrame(2);

            //Assert
            Assert.AreEqual("Cool Place", card.LocationText.text);
            Assert.IsTrue(joinShownOnHover);
            Assert.IsFalse(card.LocationGroup.activeSelf);
            Assert.AreEqual(PARCEL, joined?.position.ToParcel());
            await onlineUsers.Received(2).GetAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FallBackToTheWorldNameWhenThePlaceIsUnknown()
        {
            //Arrange
            var location = new OnlineUserData { avatarId = AMY_ID, worldName = "myworld.dcl.eth", position = Vector3.zero };
            SetOnlineUsers(location);
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Act
            presenter.Show(cts.Token);
            LobbyFriendCardView card = ShownCard(0);
            await UniTask.DelayFrame(3);

            //Assert
            Assert.AreEqual("myworld.dcl.eth", card.LocationText.text);
            await places.Received(1).GetWorldAsync(Vector2Int.zero, "myworld.dcl.eth", Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RevertToLobbyWhenTheFriendLeftBeforeJoining()
        {
            //Arrange
            SetOnlineUsers(InGenesis(AMY_ID, PARCEL));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var joinRequests = 0;
            presenter.JoinRequested = _ => joinRequests++;
            presenter.Show(cts.Token);
            LobbyFriendCardView card = ShownCard(0);
            await UniTask.DelayFrame(3);

            //Act
            SetOnlineUsers();
            card.JoinButton.onClick.Invoke();
            await UniTask.DelayFrame(2);
            card.Hover.OnPointerEnter(new PointerEventData(null));

            //Assert
            Assert.AreEqual(0, joinRequests);
            Assert.AreEqual(LOBBY, card.LocationText.text);
            Assert.IsFalse(card.JoinButton.gameObject.activeSelf);
        }

        [Test]
        public async Task RemoveFriendsGoingOfflineAndHideAtZero()
        {
            //Arrange
            Profile.CompactInfo amy = Friend(AMY_ID, "Amy");
            eventBus.BroadcastFriendConnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            presenter.Show(cts.Token);
            Assert.IsTrue(section.gameObject.activeSelf);

            //Act
            eventBus.BroadcastFriendDisconnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Assert
            Assert.IsFalse(section.gameObject.activeSelf);
            Assert.AreEqual("0 Online", section.OnlineCountText.text);
        }

        [Test]
        public async Task OpenThePassportWhenTheCardIsClicked()
        {
            //Arrange
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            presenter.Show(cts.Token);

            //Act
            ShownCard(0).Button.onClick.Invoke();

            //Assert
            await passport.Received(1).ShowAsync(AMY_ID);
        }

        [Test]
        public async Task PageTheRailWithItsArrows()
        {
            //Arrange
            for (var i = 0; i < 5; i++)
                eventBus.BroadcastFriendConnected(Friend($"0x00000000000000000000000000000000000000{i:D2}", $"Friend{i}"));

            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            LobbyRailArrowsView arrows = TestRailArrows.Awaken(section.Rail);
            presenter.Show(cts.Token);

            //Act
            arrows.Next.onClick.Invoke();

            //Assert
            Assert.AreEqual(1, section.Rail.CurrentPage);
            Assert.IsTrue(arrows.Previous.gameObject.activeSelf);
            Assert.IsFalse(arrows.Next.gameObject.activeSelf);
        }

        [Test]
        public async Task ShowOneDotPerPageOfFourCards()
        {
            //Arrange
            for (var i = 0; i < 5; i++)
                eventBus.BroadcastFriendConnected(Friend($"0x00000000000000000000000000000000000000{i:D2}", $"Friend{i}"));

            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Act
            presenter.Show(cts.Token);

            //Assert
            Assert.AreEqual("5 Online", section.OnlineCountText.text);
            Assert.AreEqual(2, ActiveDots());
            Assert.AreEqual(0, section.Rail.CurrentPage);
        }

        [Test]
        public async Task IgnoreLocationsResolvedAfterHiding()
        {
            //Arrange
            var pending = new UniTaskCompletionSource<IReadOnlyCollection<OnlineUserData>>();
            onlineUsers.GetAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(_ => pending.Task);
            places.GetPlaceAsync(PARCEL, Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(CreatePlace("Cool Place", PARCEL)));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            presenter.Show(cts.Token);
            LobbyFriendCardView card = ShownCard(0);
            await UniTask.DelayFrame(2);

            //Act
            presenter.Hide();
            cts.Cancel();
            pending.TrySetResult(new[] { InGenesis(AMY_ID, PARCEL) });
            await UniTask.DelayFrame(3);

            //Assert
            Assert.AreEqual(LOCATING, card.LocationText.text);
        }

        private LobbyFriendCardView ShownCard(int index) =>
            cardAwakener.Awaken((LobbyFriendCardView)loopList.GetShownItemByItemIndex(index).UserObjectData);

        private int ActiveDots()
        {
            var count = 0;

            for (var i = 0; i < dots.childCount; i++)
                if (dots.GetChild(i).gameObject.activeSelf)
                    count++;

            return count;
        }

        private void SetOnlineUsers(params OnlineUserData[] users) =>
            onlineUsers.GetAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
                       .Returns(UniTask.FromResult<IReadOnlyCollection<OnlineUserData>>(users));

        private static OnlineUserData InGenesis(string userId, Vector2Int parcel) =>
            new () { avatarId = userId, worldName = null, position = new Vector3((parcel.x * 16) + 8, 0, (parcel.y * 16) + 8) };

        private static Profile.CompactInfo Friend(string userId, string name) =>
            new (UserId.New(userId).Unwrap(), name);

        private static PlacesData.PlaceInfo CreatePlace(string title, Vector2Int basePosition) =>
            new (basePosition)
            {
                id = title,
                title = title,
                image = string.Empty,
                contact_name = "creator",
                world_name = string.Empty,
                base_position_processed = basePosition,
            };

        private LobbyFriendsSectionView CreateSection(Transform parent)
        {
            var sectionGo = new GameObject("Friends", typeof(RectTransform));
            sectionGo.transform.SetParent(parent);
            LobbyFriendsSectionView sectionView = sectionGo.AddComponent<LobbyFriendsSectionView>();

            var railGo = new GameObject("Rail", typeof(RectTransform));
            railGo.transform.SetParent(sectionGo.transform);
            ((RectTransform)railGo.transform).sizeDelta = new Vector2(528f, 132f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(railGo.transform);

            ScrollRect scrollRect = railGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.viewport = (RectTransform)railGo.transform;
            scrollRect.content = (RectTransform)contentGo.transform;

            loopList = railGo.AddComponent<LoopListView2>();
            loopList.ArrangeType = ListItemArrangeType.LeftToRight;
            loopList.ItemPrefabDataList.Add(new ItemPrefabConfData { mItemPrefab = CreateCardTemplate(parent), mPadding = 8 });

            dots = new GameObject("Dots", typeof(RectTransform)).transform;
            dots.SetParent(sectionGo.transform);
            var dotGo = new GameObject("DotTemplate", typeof(RectTransform));
            dotGo.transform.SetParent(dots);
            Image dotTemplate = dotGo.AddComponent<Image>();
            dotGo.SetActive(false);
            LobbyCarouselDotsView dotsView = dots.gameObject.AddComponent<LobbyCarouselDotsView>();
            SetField(dotsView, "dotTemplate", dotTemplate);

            // The rail tunes its scroll rect in Awake, so the object stays inactive until the fields are assigned
            railGo.SetActive(false);
            LobbyFriendsRailView rail = railGo.AddComponent<LobbyFriendsRailView>();
            SetField(rail, "scrollRect", scrollRect);
            SetField(rail, "cardsPerPage", 4);
            SetField(rail, "dots", dotsView);
            SetField(rail, "loopList", loopList);
            TestRailArrows.Attach(rail);
            railGo.SetActive(true);

            SetBackingField(sectionView, nameof(LobbyFriendsSectionView.OnlineCountText), CreateText(sectionGo.transform, "OnlineCount"));
            SetBackingField(sectionView, nameof(LobbyFriendsSectionView.Rail), rail);

            return sectionView;
        }

        // Inactive like a prefab asset, so the card's Awake never runs here while its fields are still unassigned
        private static GameObject CreateCardTemplate(Transform parent)
        {
            var cardGo = new GameObject("LobbyFriendCard", typeof(RectTransform));
            cardGo.transform.SetParent(parent);
            ((RectTransform)cardGo.transform).sizeDelta = new Vector2(124f, 132f);
            cardGo.SetActive(false);
            cardGo.AddComponent<LoopListViewItem2>();

            LobbyFriendCardView card = cardGo.AddComponent<LobbyFriendCardView>();
            var locationGroup = new GameObject("Location", typeof(RectTransform));
            locationGroup.transform.SetParent(cardGo.transform);

            SetBackingField(card, nameof(LobbyFriendCardView.Button), cardGo.AddComponent<Button>());
            SetBackingField(card, nameof(LobbyFriendCardView.Hover), cardGo.AddComponent<HoverableUiElement>());
            SetBackingField(card, nameof(LobbyFriendCardView.ProfilePicture), CreateProfilePicture(cardGo.transform));
            SetBackingField(card, nameof(LobbyFriendCardView.OnlineIndicator), CreateImage(cardGo.transform, "OnlineIndicator"));
            SetBackingField(card, nameof(LobbyFriendCardView.UserName), CreateUserName(cardGo.transform));
            SetBackingField(card, nameof(LobbyFriendCardView.LocationGroup), locationGroup);
            SetBackingField(card, nameof(LobbyFriendCardView.LocationText), CreateText(locationGroup.transform, "Text"));
            SetBackingField(card, nameof(LobbyFriendCardView.JoinButton), CreateButton(cardGo.transform, "Join"));
            SetField(card, "onlineStatusConfiguration", CreateStatusConfiguration());

            return cardGo;
        }

        private static ProfilePictureView CreateProfilePicture(Transform parent)
        {
            var pictureGo = new GameObject("Picture", typeof(RectTransform));
            pictureGo.transform.SetParent(parent);
            ProfilePictureView picture = pictureGo.AddComponent<ProfilePictureView>();

            var thumbnailGo = new GameObject("Thumbnail", typeof(RectTransform));
            thumbnailGo.transform.SetParent(pictureGo.transform);
            Image image = thumbnailGo.AddComponent<Image>();
            ImageView thumbnail = thumbnailGo.AddComponent<ImageView>();
            SetBackingField(thumbnail, nameof(ImageView.Image), image);
            var loadingGo = new GameObject("Loading", typeof(RectTransform));
            loadingGo.transform.SetParent(thumbnailGo.transform);
            SetBackingField(thumbnail, "LoadingObject", loadingGo);
            SetField(picture, "thumbnailImageView", thumbnail);

            return picture;
        }

        private static SimpleUserNameElement CreateUserName(Transform parent)
        {
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(parent);
            SimpleUserNameElement element = nameGo.AddComponent<SimpleUserNameElement>();
            SetField(element, "userNameText", CreateText(nameGo.transform, "UserName"));
            SetField(element, "userNameHashtagText", CreateText(nameGo.transform, "Tag"));
            SetField(element, "verifiedMark", new GameObject("Verified"));
            return element;
        }

        private static OnlineStatusConfiguration CreateStatusConfiguration()
        {
            var configuration = ScriptableObject.CreateInstance<OnlineStatusConfiguration>();

            SetField(configuration, "onlineStatusConfigurations", new List<StatusConfiguration>
            {
                new () { Status = OnlineStatus.Online, Configuration = new OnlineStatusConfigurationData { StatusColor = Color.green } },
                new () { Status = OnlineStatus.Away, Configuration = new OnlineStatusConfigurationData { StatusColor = Color.yellow } },
                new () { Status = OnlineStatus.Offline, Configuration = new OnlineStatusConfigurationData { StatusColor = Color.gray } },
            });

            return configuration;
        }

        private static Image CreateImage(Transform parent, string name)
        {
            var imageGo = new GameObject(name, typeof(RectTransform));
            imageGo.transform.SetParent(parent);
            return imageGo.AddComponent<Image>();
        }

        private static TMP_Text CreateText(Transform parent, string name)
        {
            var textGo = new GameObject(name, typeof(RectTransform));
            textGo.transform.SetParent(parent);
            return textGo.AddComponent<TextMeshProUGUI>();
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform));
            buttonGo.transform.SetParent(parent);
            return buttonGo.AddComponent<Button>();
        }

        private static void SetBackingField(object target, string propertyName, object value) =>
            SetField(target, $"<{propertyName}>k__BackingField", value);

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

            throw new ArgumentException($"Field {fieldName} not found on {target.GetType().Name}");
        }
    }
}
