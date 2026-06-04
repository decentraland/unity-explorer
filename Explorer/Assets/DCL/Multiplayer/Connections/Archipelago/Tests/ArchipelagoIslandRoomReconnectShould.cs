using DCL.Multiplayer.Connections.Archipelago.Rooms;
using NUnit.Framework;
using System;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    public class ArchipelagoIslandRoomReconnectShould
    {
        private static readonly DateTime NOW = new (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ConnectWhenNewStringArrivesRegardlessOfRoomState([Values(true, false)] bool roomIsDisconnected)
        {
            // Arrange: a backoff in the future must not delay a server-pushed string
            DateTime nextAttempt = NOW + TimeSpan.FromSeconds(30);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(isNewString: true, roomIsDisconnected, NOW, nextAttempt);

            // Assert
            Assert.IsTrue(shouldConnect);
        }

        [Test]
        public void ReconnectWithCachedStringWhenRoomIsDisconnectedAndBackoffElapsed()
        {
            // Arrange
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(1);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(isNewString: false, roomIsDisconnected: true, NOW, nextAttempt);

            // Assert
            Assert.IsTrue(shouldConnect);
        }

        [Test]
        public void SkipReconnectWhenWithinBackoff()
        {
            // Arrange
            DateTime nextAttempt = NOW + TimeSpan.FromSeconds(3);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(isNewString: false, roomIsDisconnected: true, NOW, nextAttempt);

            // Assert
            Assert.IsFalse(shouldConnect);
        }

        [Test]
        public void SkipWhenRoomIsHealthy()
        {
            // Arrange: no pending string and the room is connected — nothing to do, even past backoff
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(10);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(isNewString: false, roomIsDisconnected: false, NOW, nextAttempt);

            // Assert
            Assert.IsFalse(shouldConnect);
        }
    }
}
