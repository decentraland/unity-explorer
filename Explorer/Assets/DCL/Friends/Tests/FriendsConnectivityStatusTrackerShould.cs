using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using ECS.TestSuite;
using System.Collections.Generic;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Friends.Tests
{
    public class FriendsConnectivityStatusTrackerShould
    {
        // The tracker debounces status changes for 2000ms; the margin absorbs editor loop jitter
        private const int DEBOUNCE_WAIT_MS = 2500;
        private const string FRIEND_ID = "0x79fdd6f8ba257bda1d5a2a413ae0b43ec300ed10";
        private const string AWAY_FRIEND_ID = "0x0000000000000000000000000000000000000002";
        private const string OFFLINE_FRIEND_ID = "0x0000000000000000000000000000000000000003";

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
        [Test]
        public async Task CopyOnlineAndAwayFriendsOnly()
        {
            //Arrange
            var onlineFriend = new Profile.CompactInfo(UserId.New(FRIEND_ID).Unwrap(), "Online");
            var awayFriend = new Profile.CompactInfo(UserId.New(AWAY_FRIEND_ID).Unwrap(), "Away");
            var offlineFriend = new Profile.CompactInfo(UserId.New(OFFLINE_FRIEND_ID).Unwrap(), "Offline");
            eventBus.BroadcastFriendConnected(onlineFriend);
            eventBus.BroadcastFriendAsAway(awayFriend);
            eventBus.BroadcastFriendConnected(offlineFriend);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);
            eventBus.BroadcastFriendDisconnected(offlineFriend);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Act
            var copy = new List<Profile.CompactInfo>();
            tracker.CopyOnlineFriendsTo(copy);

            //Assert
            Assert.AreEqual(2, copy.Count);
            Assert.IsTrue(copy.Contains(onlineFriend));
            Assert.IsTrue(copy.Contains(awayFriend));
        }

        [Test]
        public async Task RaiseFriendRemovedAndDropFromOnlineCopy()
        {
            //Arrange
            var friendProfile = new Profile.CompactInfo(UserId.New(FRIEND_ID).Unwrap(), "TestFriend");
            var removed = new List<string>();
            tracker.OnFriendRemoved += removed.Add;
            eventBus.BroadcastFriendConnected(friendProfile);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Act
            eventBus.BroadcastThatYouRemovedFriend(friendProfile.UserId.Value);
            eventBus.BroadcastThatOtherUserRemovedTheFriendship(OFFLINE_FRIEND_ID);

            //Assert
            var copy = new List<Profile.CompactInfo>();
            tracker.CopyOnlineFriendsTo(copy);
            Assert.AreEqual(0, copy.Count);
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(friendProfile.UserId.Value, removed[0]);
        }

        [Test]
        public async Task RaiseResetAndEmptyOnlineCopy()
        {
            //Arrange
            var friendProfile = new Profile.CompactInfo(UserId.New(FRIEND_ID).Unwrap(), "TestFriend");
            var resets = 0;
            tracker.OnReset += () => resets++;
            eventBus.BroadcastFriendConnected(friendProfile);
            await UniTask.Delay(DEBOUNCE_WAIT_MS);

            //Act
            tracker.Reset();

            //Assert
            var copy = new List<Profile.CompactInfo>();
            tracker.CopyOnlineFriendsTo(copy);
            Assert.AreEqual(0, copy.Count);
            Assert.AreEqual(1, resets);
        }
    }
}
