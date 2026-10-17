using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.RoomHubs;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Chat.ChatServices.Tests
{
    [TestFixture]
    public class NearbyUserStateServiceShould
    {
        private const string WALLET = "0xaaaa000000000000000000000000000000000001";
        private const string BLOCKED_WALLET = "0xbbbb000000000000000000000000000000000002";
        private const string PRESENTATION_BOT = "presentation-bot:theatre";
        private const string CAST_VIEWER = "viewer-7f3a";

        [Test]
        public void ExcludeNonWalletParticipantsFromOnlineUsers()
        {
            // Arrange
            IRoomHub roomHub = Substitute.For<IRoomHub>();
            roomHub.AllLocalRoomsRemoteParticipantIdentities().Returns(new HashSet<string> { WALLET, BLOCKED_WALLET, PRESENTATION_BOT, CAST_VIEWER });

            IUserBlockingCache userBlockingCache = Substitute.For<IUserBlockingCache>();
            userBlockingCache.UserIsBlocked(BLOCKED_WALLET).Returns(true);

            var service = new NearbyUserStateService(roomHub, new ChatEventBus(), userBlockingCache);

            // Act
            service.Activate();

            // Assert
            Assert.That(service.OnlineParticipants, Is.EquivalentTo(new[] { WALLET }));

            service.Deactivate();
        }
    }
}
