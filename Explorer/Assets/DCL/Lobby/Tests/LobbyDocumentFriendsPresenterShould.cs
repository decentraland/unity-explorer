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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Utility;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyDocumentFriendsPresenterShould
    {
        // The tracker debounces status changes for 2000ms; the margin absorbs editor loop jitter
        private const int DEBOUNCE_WAIT_MS = 2500;
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyFriendCard.uxml";
        private const string AMY_ID = "0x0000000000000000000000000000000000000a11";
        private const string ZED_ID = "0x0000000000000000000000000000000000000b22";
        private const string LOCATING = "Locating…";
        private const string LOBBY = "Lobby";

        private static readonly Vector2Int PARCEL = new (5, 7);

        private DefaultFriendsEventBus eventBus = null!;
        private FriendsConnectivityStatusTracker tracker = null!;
        private IOnlineUsersProvider onlineUsers = null!;
        private IPlacesAPIService places = null!;
        private IPassportBridge passport = null!;
        private VisualElement section = null!;
        private CancellationTokenSource cts = null!;
        private LobbyDocumentFriendsPresenter presenter = null!;

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

            eventBus = new DefaultFriendsEventBus();
            tracker = new FriendsConnectivityStatusTracker(eventBus, isConnectivityStatusEnabled: true);
            onlineUsers = Substitute.For<IOnlineUsersProvider>();
            SetOnlineUsers();
            places = Substitute.For<IPlacesAPIService>();
            places.GetPlaceAsync(Arg.Any<Vector2Int>(), Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(null));
            places.GetWorldAsync(Arg.Any<Vector2Int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(null));
            passport = Substitute.For<IPassportBridge>();
            passport.ShowAsync(Arg.Any<string>()).Returns(UniTask.CompletedTask);

            section = CreateSection();
            cts = new CancellationTokenSource();

            var rail = new LobbyFriendsRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
            presenter = new LobbyDocumentFriendsPresenter(rail, tracker, onlineUsers, places, passport);
        }

        [TearDown]
        public void TearDown()
        {
            GetProfileThumbnailCommand.Reset();
            presenter.Dispose();
            cts.Cancel();
            cts.Dispose();
            tracker.Dispose();
        }

        [Test]
        public void HideSectionWithoutOnlineFriends()
        {
            // Act
            presenter.Show(section, cts.Token);

            // Assert
            Assert.AreEqual(DisplayStyle.None, section.style.display.value);
            Assert.AreEqual("0 Online", OnlineCount().text);
        }

        [Test]
        public async Task ShowOnlineFriendsSortedByNameAndReadLobbyWhenTheyHaveNoPosition()
        {
            // Arrange
            eventBus.BroadcastFriendConnected(Friend(ZED_ID, "Zed"));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            // Act
            presenter.Show(section, cts.Token);
            LobbyFriendCardElement card = Card(0);
            string initialLocation = card.Location;
            await UniTask.DelayFrame(3);

            // Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual("2 Online", OnlineCount().text);
            Assert.AreEqual("Amy", card.UserName);
            Assert.AreEqual("Zed", Card(1).UserName);
            Assert.AreEqual(LOCATING, initialLocation);
            Assert.AreEqual(LOBBY, card.Location);
            Assert.IsFalse(card.CanJoin);
        }

        [Test]
        public async Task ShowPlaceTitleAndJoinWithTheLivePosition()
        {
            // Arrange
            OnlineUserData location = InGenesis(AMY_ID, PARCEL);
            SetOnlineUsers(location);
            places.GetPlaceAsync(PARCEL, Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(CreatePlace("Cool Place", PARCEL)));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            OnlineUserData? joined = null;
            presenter.JoinRequested = data => joined = data;

            // Act
            presenter.Show(section, cts.Token);
            LobbyFriendCardElement card = Card(0);
            await UniTask.DelayFrame(3);
            bool joinable = card.CanJoin;
            card.JoinClicked!.Invoke();
            await UniTask.DelayFrame(2);

            // Assert
            Assert.AreEqual("Cool Place", card.Location);
            Assert.IsTrue(joinable);
            Assert.AreEqual(PARCEL, joined?.position.ToParcel());
            await onlineUsers.Received(2).GetAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FallBackToTheWorldNameWhenThePlaceIsUnknown()
        {
            // Arrange
            var location = new OnlineUserData { avatarId = AMY_ID, worldName = "myworld.dcl.eth", position = Vector3.zero };
            SetOnlineUsers(location);
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            // Act
            presenter.Show(section, cts.Token);
            LobbyFriendCardElement card = Card(0);
            await UniTask.DelayFrame(3);

            // Assert
            Assert.AreEqual("myworld.dcl.eth", card.Location);
            await places.Received(1).GetWorldAsync(Vector2Int.zero, "myworld.dcl.eth", Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RevertToLobbyWhenTheFriendLeftBeforeJoining()
        {
            // Arrange
            SetOnlineUsers(InGenesis(AMY_ID, PARCEL));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var joinRequests = 0;
            presenter.JoinRequested = _ => joinRequests++;
            presenter.Show(section, cts.Token);
            LobbyFriendCardElement card = Card(0);
            await UniTask.DelayFrame(3);

            // Act
            SetOnlineUsers();
            card.JoinClicked!.Invoke();
            await UniTask.DelayFrame(2);

            // Assert
            Assert.AreEqual(0, joinRequests);
            Assert.AreEqual(LOBBY, card.Location);
            Assert.IsFalse(card.CanJoin);
        }

        [Test]
        public async Task RemoveFriendsGoingOfflineAndHideAtZero()
        {
            // Arrange
            Profile.CompactInfo amy = Friend(AMY_ID, "Amy");
            eventBus.BroadcastFriendConnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            presenter.Show(section, cts.Token);
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);

            // Act
            eventBus.BroadcastFriendDisconnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            // Assert
            Assert.AreEqual(DisplayStyle.None, section.style.display.value);
            Assert.AreEqual("0 Online", OnlineCount().text);
        }

        [Test]
        public async Task OpenThePassportWhenTheCardIsClicked()
        {
            // Arrange
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            presenter.Show(section, cts.Token);

            // Act
            Card(0).Clicked!.Invoke();

            // Assert
            await passport.Received(1).ShowAsync(AMY_ID);
        }

        [Test]
        public async Task IgnoreLocationsResolvedAfterHiding()
        {
            // Arrange
            var pending = new UniTaskCompletionSource<IReadOnlyCollection<OnlineUserData>>();
            onlineUsers.GetAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(_ => pending.Task);
            places.GetPlaceAsync(PARCEL, Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(UniTask.FromResult<PlacesData.PlaceInfo?>(CreatePlace("Cool Place", PARCEL)));
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            presenter.Show(section, cts.Token);
            LobbyFriendCardElement card = Card(0);
            await UniTask.DelayFrame(2);

            // Act
            presenter.Hide();
            cts.Cancel();
            pending.TrySetResult(new[] { InGenesis(AMY_ID, PARCEL) });
            await UniTask.DelayFrame(3);

            // Assert
            Assert.AreEqual(LOCATING, card.Location);
        }

        private LobbyFriendCardElement Card(int index) =>
            (LobbyFriendCardElement)section.Q<LobbyRailElement>()[index];

        private Label OnlineCount() =>
            section.Q<Label>("OnlineCount");

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

        // The header and the rail of the document's friends section, without its stylesheet
        private static VisualElement CreateSection()
        {
            var friendsSection = new VisualElement { name = "Friends" };
            friendsSection.Add(new Label { name = "OnlineCount", text = "0 Online" });
            friendsSection.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 4 });
            return friendsSection;
        }
    }
}
