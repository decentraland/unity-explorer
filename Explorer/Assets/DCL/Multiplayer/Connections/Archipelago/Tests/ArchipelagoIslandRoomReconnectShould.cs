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
        public void ConsumePendingBecomesCurrentKeepingTheString()
        {
            ConnectionStringState consumed =
                ConnectionStringState.FromPendingConnection(new PendingConnection("conn-str")).Consume();

            Assert.IsTrue(consumed.IsCurrentConnection(out CurrentConnection current));
            Assert.AreEqual("conn-str", current.ConnectionString);
        }

        [Test]
        public void ConsumeNoneStaysNone()
        {
            ConnectionStringState consumed = ConnectionStringState.None().Consume();

            bool isNone = consumed.Match(
                onNone: static () => true,
                onPendingConnection: static _ => false,
                onCurrentConnection: static _ => false);

            Assert.IsTrue(isNone);
        }

        [Test]
        public void ConsumeIsIdempotentOnceCurrent()
        {
            // A Current string is only re-evaluated against the room/backoff state, never re-consumed
            ConnectionStringState current =
                ConnectionStringState.FromPendingConnection(new PendingConnection("conn-str")).Consume();

            ConnectionStringState reconsumed = current.Consume();

            Assert.IsTrue(reconsumed.IsCurrentConnection(out CurrentConnection currentConnection));
            Assert.AreEqual("conn-str", currentConnection.ConnectionString);
        }
    }
}
