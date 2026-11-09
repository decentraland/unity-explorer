using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyConnectedFriendsPresenterShould
    {
        // Covers the tracker's 2000ms debounce plus editor loop jitter
        private const int DEBOUNCE_WAIT_MS = 2500;
        private const string AMY_ID = "0x0000000000000000000000000000000000000a11";
        private const string ZED_ID = "0x0000000000000000000000000000000000000b22";
        private const string STRANGER_ID = "0x0000000000000000000000000000000000000c33";

        private DefaultFriendsEventBus eventBus = null!;
        private FriendsConnectivityStatusTracker tracker = null!;
        private VisualElement tooltip = null!;
        private Label tooltipLabel = null!;
        private CancellationTokenSource cts = null!;
        private LobbyConnectedFriendsPresenter presenter = null!;
        private GameObject? documentGameObject;
        private PanelSettings? panelSettings;

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
            GetProfileThumbnailCommand.Reset();

            GetProfileThumbnailCommand.Initialize(new GetProfileThumbnailCommand(new ProfileRepositoryWrapper(Substitute.For<IProfileRepository>(),
                Substitute.For<IProfileCache>(), Substitute.For<ISpriteCache>(), Substitute.For<IWeb3IdentityCache>())));

            eventBus = new DefaultFriendsEventBus();
            tracker = new FriendsConnectivityStatusTracker(eventBus, isConnectivityStatusEnabled: true);

            tooltipLabel = new Label { name = "Label" };
            tooltip = new VisualElement { name = "FriendTooltip" };
            tooltip.Add(tooltipLabel);
            cts = new CancellationTokenSource();

            presenter = new LobbyConnectedFriendsPresenter(tracker);
        }

        [TearDown]
        public void TearDown()
        {
            GetProfileThumbnailCommand.Reset();
            presenter.Dispose();
            cts.Cancel();
            cts.Dispose();
            tracker.Dispose();

            if (documentGameObject != null)
                Object.DestroyImmediate(documentGameObject);

            if (panelSettings != null)
                Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public async Task ShowOnlyTheOnlineFriendsAmongTheConnectedAddresses()
        {
            //Arrange
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            eventBus.BroadcastFriendConnected(Friend(ZED_ID, "Zed"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            presenter.Show(tooltip, cts.Token);

            //Act
            presenter.Bind(row, new[] { AMY_ID, STRANGER_ID });

            //Assert
            Assert.AreEqual(1, row.Count);
        }

        [Test]
        public async Task MatchTheAddressesRegardlessOfTheirCase()
        {
            //Arrange
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            presenter.Show(tooltip, cts.Token);

            //Act
            presenter.Bind(row, new[] { AMY_ID.ToUpperInvariant() });

            //Assert
            Assert.AreEqual(1, row.Count);
        }

        [Test]
        public async Task CountTheFriendsPastTheSlots()
        {
            //Arrange
            for (var i = 0; i < 5; i++)
                eventBus.BroadcastFriendConnected(Friend(FriendId(i), $"Friend {i}"));

            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            presenter.Show(tooltip, cts.Token);

            //Act
            presenter.Bind(row, new[] { FriendId(0), FriendId(1), FriendId(2), FriendId(3), FriendId(4) });

            //Assert
            Assert.AreEqual(5, row.Count);
            Assert.AreEqual("+2", row.Q<Label>().text);
        }

        [Test]
        public async Task HideTheRowWithoutAddresses()
        {
            //Arrange
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            presenter.Show(tooltip, cts.Token);
            presenter.Bind(row, new[] { AMY_ID });

            //Act
            presenter.Bind(row, null);

            //Assert
            Assert.AreEqual(0, row.Count);
        }

        [Test]
        public async Task RefreshTheBoundRowsAsFriendsComeAndGo()
        {
            //Arrange
            Profile.CompactInfo amy = Friend(AMY_ID, "Amy");
            var row = new LobbyConnectedFriendsElement();
            presenter.Show(tooltip, cts.Token);
            presenter.Bind(row, new[] { AMY_ID });
            Assert.AreEqual(0, row.Count);

            //Act
            eventBus.BroadcastFriendConnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Assert
            Assert.AreEqual(1, row.Count);

            //Act
            eventBus.BroadcastFriendDisconnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Assert
            Assert.AreEqual(0, row.Count);
        }

        [Test]
        public async Task ClearTheRowsWhenTheTrackerResets()
        {
            //Arrange
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            presenter.Show(tooltip, cts.Token);
            presenter.Bind(row, new[] { AMY_ID });

            //Act
            tracker.Reset();

            //Assert
            Assert.AreEqual(0, row.Count);
        }

        [Test]
        public async Task IgnoreTheTrackerWhileHiddenAndCatchUpOnShow()
        {
            //Arrange
            Profile.CompactInfo amy = Friend(AMY_ID, "Amy");
            eventBus.BroadcastFriendConnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            presenter.Show(tooltip, cts.Token);
            presenter.Bind(row, new[] { AMY_ID });

            //Act
            presenter.Hide();
            eventBus.BroadcastFriendDisconnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Assert
            Assert.AreEqual(1, row.Count, "A hidden presenter leaves the rows as they were");

            //Act
            presenter.Show(tooltip, cts.Token);

            //Assert
            Assert.AreEqual(0, row.Count);
        }

        [Test]
        public async Task NameTheHoveredFriendInTheTooltip()
        {
            //Arrange
            eventBus.BroadcastFriendConnected(Friend(AMY_ID, "Amy"));
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            AttachToPanel(row, tooltip);
            presenter.Show(tooltip, cts.Token);
            presenter.Bind(row, new[] { AMY_ID });
            VisualElement slot = row.Slot(0);

            //Act
            Send(slot, PointerEnterEvent.GetPooled());

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, tooltip.style.display.value);
            Assert.AreEqual("Amy", tooltipLabel.text);

            //Act
            Send(slot, PointerLeaveEvent.GetPooled());

            //Assert
            Assert.AreEqual(DisplayStyle.None, tooltip.style.display.value);
        }

        [Test]
        public async Task HideTheTooltipWhenTheHoveredFriendLeaves()
        {
            //Arrange
            Profile.CompactInfo amy = Friend(AMY_ID, "Amy");
            eventBus.BroadcastFriendConnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            var row = new LobbyConnectedFriendsElement();
            AttachToPanel(row, tooltip);
            presenter.Show(tooltip, cts.Token);
            presenter.Bind(row, new[] { AMY_ID });
            Send(row.Slot(0), PointerEnterEvent.GetPooled());

            //Act
            eventBus.BroadcastFriendDisconnected(amy);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Assert
            Assert.AreEqual(DisplayStyle.None, tooltip.style.display.value);
        }

        private void AttachToPanel(params VisualElement[] elements)
        {
            documentGameObject = new GameObject(nameof(LobbyConnectedFriendsPresenterShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;

            foreach (VisualElement element in elements)
                document.rootVisualElement.Add(element);
        }

        // Dispatched straight to the slot; the position is irrelevant as the panel is not laid out
        private static void Send<T>(VisualElement target, T evt) where T: EventBase<T>, new()
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static string FriendId(int index) =>
            $"0x00000000000000000000000000000000000000{index:x2}";

        private static Profile.CompactInfo Friend(string id, string name) =>
            new (UserId.New(id).Unwrap(), name);
    }
}
