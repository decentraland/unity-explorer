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

        [Test]
        public void NotForceFreshHandshakeBelowFailureThreshold([Values(0, 1, 2)] int consecutiveFailures) =>
            // Act & Assert: fewer than 3 consecutive failures keep retrying the cached string
            Assert.IsFalse(ArchipelagoIslandRoom.ShouldForceFreshHandshake(consecutiveFailures));

        [Test]
        public void ForceFreshHandshakeWhenFailureThresholdReached([Values(3, 4)] int consecutiveFailures) =>
            // Act & Assert: at/above 3 consecutive failures the cached string is abandoned for a fresh handshake
            Assert.IsTrue(ArchipelagoIslandRoom.ShouldForceFreshHandshake(consecutiveFailures));

        [Test]
        public void ConsumeNewPendingBecomesCurrentKeepingTheString()
        {
            ArchipelagoIslandRoom.ConnectionStringState consumed =
                ArchipelagoIslandRoom.ConnectionStringState.NewPending("conn-str").Consume();

            Assert.AreEqual(ArchipelagoIslandRoom.ConnectionStringState.Kind.CURRENT, consumed.State);
            Assert.AreEqual("conn-str", consumed.ConnectionString);
        }

        [Test]
        public void ConsumeNoneStaysNone()
        {
            ArchipelagoIslandRoom.ConnectionStringState consumed = ArchipelagoIslandRoom.ConnectionStringState.None.Consume();

            Assert.AreEqual(ArchipelagoIslandRoom.ConnectionStringState.Kind.NONE, consumed.State);
            Assert.IsNull(consumed.ConnectionString);
        }

        [Test]
        public void ConsumeIsIdempotentOnceCurrent()
        {
            // A Current string is only re-evaluated against the room/backoff state, never re-consumed
            ArchipelagoIslandRoom.ConnectionStringState current =
                ArchipelagoIslandRoom.ConnectionStringState.NewPending("conn-str").Consume();

            ArchipelagoIslandRoom.ConnectionStringState reconsumed = current.Consume();

            Assert.AreEqual(ArchipelagoIslandRoom.ConnectionStringState.Kind.CURRENT, reconsumed.State);
            Assert.AreEqual("conn-str", reconsumed.ConnectionString);
        }
    }
}
