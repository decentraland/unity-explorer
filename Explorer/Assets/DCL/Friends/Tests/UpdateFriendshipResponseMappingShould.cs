using DCL.Profiles.Self;
using DCL.SocialService;
using Decentraland.SocialService;
using Decentraland.SocialService.V2;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Friends.Tests
{
    public class UpdateFriendshipResponseMappingShould
    {
        [Test]
        public void ReturnAcceptedPayloadOnAcceptedResponse()
        {
            var accepted = new UpsertFriendshipResponse.Types.Accepted { Id = "req-1", CreatedAt = 123 };
            var response = new UpsertFriendshipResponse { Accepted = accepted };

            Assert.That(RPCFriendsService.ToAccepted(response), Is.SameAs(accepted));
        }

        [Test]
        public void ReturnNullOnInvalidFriendshipAction()
        {
            var response = new UpsertFriendshipResponse
            {
                InvalidFriendshipAction = new InvalidFriendshipAction { Message = "You cannot send a friendship request to yourself" },
            };

            Assert.That(RPCFriendsService.ToAccepted(response), Is.Null);
        }

        [Test]
        public void ReturnNullOnInvalidRequest()
        {
            var response = new UpsertFriendshipResponse
            {
                InvalidRequest = new InvalidRequest { Message = "malformed" },
            };

            Assert.That(RPCFriendsService.ToAccepted(response), Is.Null);
        }

        [Test]
        public void ThrowOnInternalServerError()
        {
            var response = new UpsertFriendshipResponse
            {
                InternalServerError = new InternalServerError { Message = "boom" },
            };

            Assert.That(() => RPCFriendsService.ToAccepted(response), Throws.Exception);
        }
    }

    public class FriendshipCacheMutationShould
    {
        private const string FRIEND_ID = "0xfriend";

        private IFriendsEventBus eventBus = null!;
        private FriendsCache friendsCache = null!;
        private RPCFriendsService service = null!;

        [SetUp]
        public void SetUp()
        {
            eventBus = Substitute.For<IFriendsEventBus>();
            friendsCache = new FriendsCache();
            service = new RPCFriendsService(eventBus, friendsCache, Substitute.For<ISelfProfile>(), Substitute.For<IRPCSocialServices>());
        }

        [Test]
        public void NotAcceptWhenServerRejectsTheAction()
        {
            bool applied = service.ApplyAcceptedFriendship(null, FRIEND_ID);

            Assert.That(applied, Is.False);
            Assert.That(friendsCache.Contains(FRIEND_ID), Is.False);
            eventBus.DidNotReceive().BroadcastThatYouAcceptedFriendRequestReceivedFromOtherUser(Arg.Any<string>());
        }

        [Test]
        public void AcceptWhenServerAcceptsTheAction()
        {
            bool applied = service.ApplyAcceptedFriendship(new UpsertFriendshipResponse.Types.Accepted(), FRIEND_ID);

            Assert.That(applied, Is.True);
            Assert.That(friendsCache.Contains(FRIEND_ID), Is.True);
            eventBus.Received(1).BroadcastThatYouAcceptedFriendRequestReceivedFromOtherUser(FRIEND_ID);
        }

        [Test]
        public void NotDeleteWhenServerRejectsTheAction()
        {
            friendsCache.Add(FRIEND_ID);

            bool applied = service.ApplyDeletedFriendship(null, FRIEND_ID);

            Assert.That(applied, Is.False);
            Assert.That(friendsCache.Contains(FRIEND_ID), Is.True);
            eventBus.DidNotReceive().BroadcastThatYouRemovedFriend(Arg.Any<string>());
        }

        [Test]
        public void DeleteWhenServerAcceptsTheAction()
        {
            friendsCache.Add(FRIEND_ID);

            bool applied = service.ApplyDeletedFriendship(new UpsertFriendshipResponse.Types.Accepted(), FRIEND_ID);

            Assert.That(applied, Is.True);
            Assert.That(friendsCache.Contains(FRIEND_ID), Is.False);
            eventBus.Received(1).BroadcastThatYouRemovedFriend(FRIEND_ID);
        }
    }
}
