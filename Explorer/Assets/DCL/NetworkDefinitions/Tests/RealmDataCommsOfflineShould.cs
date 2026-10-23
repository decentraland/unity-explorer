using NSubstitute;
using NUnit.Framework;

namespace ECS.Tests
{
    public class RealmDataCommsOfflineShould
    {
        private IRealmData realmData = null!;

        [SetUp]
        public void SetUp()
        {
            realmData = Substitute.For<IRealmData>();
        }

        [TestCase("fixed-adapter:offline:offline")]
        [TestCase("offline:offline")]
        [TestCase("FIXED-ADAPTER:OFFLINE:Offline")]
        public void BeOfflineWhenAdapterContainsOfflineMarker(string commsAdapter)
        {
            // Arrange
            realmData.CommsAdapter.Returns(commsAdapter);

            // Act
            bool isOffline = realmData.IsCommsOffline();

            // Assert
            Assert.IsTrue(isOffline);
        }

        [TestCase("fixed-adapter:signed-login:https://example.com/worlds/x/comms")]
        [TestCase("ws-room:ws://127.0.0.1:8000/mini-comms/room-1")]
        [TestCase("")]
        public void BeOnlineWhenAdapterLacksOfflineMarker(string commsAdapter)
        {
            // Arrange
            realmData.CommsAdapter.Returns(commsAdapter);

            // Act
            bool isOffline = realmData.IsCommsOffline();

            // Assert
            Assert.IsFalse(isOffline);
        }
    }
}
