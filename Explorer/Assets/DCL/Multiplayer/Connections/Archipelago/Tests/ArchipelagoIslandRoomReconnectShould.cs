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
            ConnectionStringState pending = ConnectionStringState.FromPendingConnection(new PendingConnection("conn-str"));
            DateTime nextAttempt = NOW + TimeSpan.FromSeconds(30);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(pending, roomIsDisconnected, NOW, nextAttempt, out string? connectionString);

            // Assert
            Assert.IsTrue(shouldConnect);
            Assert.AreEqual("conn-str", connectionString);
        }

        [Test]
        public void ReconnectWithCachedStringWhenRoomIsDisconnectedAndBackoffElapsed()
        {
            // Arrange
            ConnectionStringState current = ConnectionStringState.FromCurrentConnection(new CurrentConnection("conn-str"));
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(1);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(current, roomIsDisconnected: true, NOW, nextAttempt, out string? connectionString);

            // Assert
            Assert.IsTrue(shouldConnect);
            Assert.AreEqual("conn-str", connectionString);
        }

        [Test]
        public void SkipReconnectWhenWithinBackoff()
        {
            // Arrange
            ConnectionStringState current = ConnectionStringState.FromCurrentConnection(new CurrentConnection("conn-str"));
            DateTime nextAttempt = NOW + TimeSpan.FromSeconds(3);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(current, roomIsDisconnected: true, NOW, nextAttempt, out string? _);

            // Assert
            Assert.IsFalse(shouldConnect);
        }

        [Test]
        public void SkipWhenRoomIsHealthy()
        {
            // Arrange: a cached string and a connected room — nothing to do, even past backoff
            ConnectionStringState current = ConnectionStringState.FromCurrentConnection(new CurrentConnection("conn-str"));
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(10);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(current, roomIsDisconnected: false, NOW, nextAttempt, out string? _);

            // Assert
            Assert.IsFalse(shouldConnect);
        }

        [Test]
        public void SkipWhenNoStringReceived()
        {
            // Arrange: nothing pushed by the server yet — never connect, even disconnected and past backoff
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(10);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(ConnectionStringState.None(), roomIsDisconnected: true, NOW, nextAttempt, out string? _);

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

            Assert.IsTrue(consumed.IsNone());
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
