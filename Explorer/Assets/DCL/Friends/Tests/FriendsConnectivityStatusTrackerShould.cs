using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using ECS.TestSuite;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Friends.Tests
{
    public class FriendsConnectivityStatusTrackerShould
    {
        // The tracker debounces status changes for 2000ms; the margin absorbs editor loop jitter
        private const int DEBOUNCE_WAIT_MS = 2500;
        private const string FRIEND_ID = "0x79fdd6f8ba257bda1d5a2a413ae0b43ec300ed10";

        private DefaultFriendsEventBus eventBus = null!;
        private FriendsConnectivityStatusTracker tracker = null!;

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
            eventBus = new DefaultFriendsEventBus();
            tracker = new FriendsConnectivityStatusTracker(eventBus, isConnectivityStatusEnabled: true);
        }

        [TearDown]
        public void TearDown() =>
            tracker.Dispose();

        [Test]
        public async Task RaiseOnlineEventWhenSameStatusIsRebroadcastAfterReset()
        {
            //Arrange
            var friendProfile = new Profile.CompactInfo(UserId.New(FRIEND_ID).Unwrap(), "TestFriend");
            var onlineEventsCount = 0;
            tracker.OnFriendBecameOnline += _ => onlineEventsCount++;

            eventBus.BroadcastFriendConnected(friendProfile);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Act
            tracker.Reset();
            eventBus.BroadcastFriendConnected(friendProfile);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Assert
            Assert.AreEqual(2, onlineEventsCount);
            Assert.AreEqual(OnlineStatus.Online, tracker.GetFriendStatus(friendProfile.UserId));
        }

        [Test]
        public async Task ReportFriendAsOfflineAfterReset()
        {
            //Arrange
            var friendProfile = new Profile.CompactInfo(UserId.New(FRIEND_ID).Unwrap(), "TestFriend");
            eventBus.BroadcastFriendConnected(friendProfile);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            Assert.AreEqual(OnlineStatus.Online, tracker.GetFriendStatus(friendProfile.UserId));

            //Act
            tracker.Reset();

            //Assert
            Assert.AreEqual(OnlineStatus.Offline, tracker.GetFriendStatus(friendProfile.UserId));
        }

        [Test]
        public async Task CancelPendingDebounceOnReset()
        {
            //Arrange
            var friendProfile = new Profile.CompactInfo(UserId.New(FRIEND_ID).Unwrap(), "TestFriend");
            var onlineEventsCount = 0;
            tracker.OnFriendBecameOnline += _ => onlineEventsCount++;
            eventBus.BroadcastFriendConnected(friendProfile);

            //Act
            tracker.Reset();
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Assert
            Assert.AreEqual(0, onlineEventsCount);
            Assert.AreEqual(OnlineStatus.Offline, tracker.GetFriendStatus(friendProfile.UserId));
        }
    }
}
