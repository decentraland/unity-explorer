using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using NUnit.Framework;
using System;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    public class ArchipelagoIslandRoomReconnectShould
    {
        private const string HELD_ISLAND = "island-C1";
        private const string OTHER_ISLAND = "island-C2";

        private static readonly DateTime NOW = new (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ConnectWhenANewIslandArrivesRegardlessOfRoomState(
            [Values(LKConnectionState.ConnDisconnected, LKConnectionState.ConnConnected, LKConnectionState.ConnReconnecting)]
            LKConnectionState roomState)
        {
            // Arrange: a backoff in the future must not delay a server-pushed string
            ConnectionStringState pending = ConnectionStringState.FromPendingConnection(new PendingConnection(OTHER_ISLAND, "conn-str"));
            DateTime nextAttempt = NOW + TimeSpan.FromSeconds(30);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(pending, roomState, HELD_ISLAND, NOW, nextAttempt, out string? connectionString);

            // Assert
            Assert.IsTrue(shouldConnect);
            Assert.AreEqual("conn-str", connectionString);
        }

        [Test]
        public void SkipReJoiningTheIslandAlreadyHeld(
            [Values(LKConnectionState.ConnConnected, LKConnectionState.ConnReconnecting)]
            LKConnectionState roomState)
        {
            // Arrange: the server re-announced the island this client already holds, which happens on every
            // Pulse-only reconnect. A reconnecting room still holds it server-side, so both states suppress.
            ConnectionStringState pending = ConnectionStringState.FromPendingConnection(new PendingConnection(HELD_ISLAND, "fresh-conn-str"));
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(10);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(pending, roomState, HELD_ISLAND, NOW, nextAttempt, out string? _);

            // Assert
            Assert.IsFalse(shouldConnect);
        }

        [Test]
        public void ReJoinTheSameIslandOnceTheRoomIsFullyDisconnected()
        {
            // Arrange: same island, but the room dropped — a genuine reconnect, not a re-announcement
            ConnectionStringState pending = ConnectionStringState.FromPendingConnection(new PendingConnection(HELD_ISLAND, "conn-str"));
            DateTime nextAttempt = NOW + TimeSpan.FromSeconds(30);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(pending, LKConnectionState.ConnDisconnected, HELD_ISLAND, NOW, nextAttempt, out string? connectionString);

            // Assert
            Assert.IsTrue(shouldConnect);
            Assert.AreEqual("conn-str", connectionString);
        }

        /// <summary>
        ///     Pins the state machine that lets a suppressed re-announcement still refresh the cached
        ///     token: the pending value is what the predicate suppresses on, and its consumed form is what
        ///     the next reconnect retries with, carrying the same fresh string.
        ///     <para />
        ///     It does not pin the sequencing inside <c>ReadAndConsumeConnectionState</c>, which is what
        ///     actually consumes before deciding — that method is private and its owner reaches app
        ///     singletons in its base constructor, so no edit-mode test can drive it today.
        /// </summary>
        [Test]
        public void SuppressOnThePendingValueAndRetryWithItsConsumedForm()
        {
            // Arrange: a re-announcement of the held island, suppressed while the room is healthy
            ConnectionStringState pending = ConnectionStringState.FromPendingConnection(new PendingConnection(HELD_ISLAND, "fresh-conn-str"));

            // Act
            ConnectionStringState afterConsume = pending.Consume();
            bool suppressed = !ArchipelagoIslandRoom.ShouldAttemptConnection(pending, LKConnectionState.ConnConnected, HELD_ISLAND, NOW, DateTime.MinValue, out string? _);
            bool retries = ArchipelagoIslandRoom.ShouldAttemptConnection(afterConsume, LKConnectionState.ConnDisconnected, HELD_ISLAND, NOW, DateTime.MinValue, out string? retryString);

            // Assert
            Assert.IsTrue(suppressed);
            Assert.IsTrue(retries);
            Assert.AreEqual("fresh-conn-str", retryString);
        }

        [Test]
        public void ReconnectWithCachedStringWhenRoomIsDisconnectedAndBackoffElapsed()
        {
            // Arrange
            ConnectionStringState current = ConnectionStringState.FromCurrentConnection(new CurrentConnection(HELD_ISLAND, "conn-str"));
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(1);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(current, LKConnectionState.ConnDisconnected, HELD_ISLAND, NOW, nextAttempt, out string? connectionString);

            // Assert
            Assert.IsTrue(shouldConnect);
            Assert.AreEqual("conn-str", connectionString);
        }

        [Test]
        public void SkipTheCachedRetryWhileTheRoomIsStillReconnecting()
        {
            // Arrange: LiveKit is recovering the same room, so racing it with our own connect would collide
            ConnectionStringState current = ConnectionStringState.FromCurrentConnection(new CurrentConnection(HELD_ISLAND, "conn-str"));
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(10);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(current, LKConnectionState.ConnReconnecting, HELD_ISLAND, NOW, nextAttempt, out string? _);

            // Assert
            Assert.IsFalse(shouldConnect);
        }

        [Test]
        public void SkipReconnectWhenWithinBackoff()
        {
            // Arrange
            ConnectionStringState current = ConnectionStringState.FromCurrentConnection(new CurrentConnection(HELD_ISLAND, "conn-str"));
            DateTime nextAttempt = NOW + TimeSpan.FromSeconds(3);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(current, LKConnectionState.ConnDisconnected, HELD_ISLAND, NOW, nextAttempt, out string? _);

            // Assert
            Assert.IsFalse(shouldConnect);
        }

        [Test]
        public void SkipWhenRoomIsHealthy()
        {
            // Arrange: a cached string and a connected room — nothing to do, even past backoff
            ConnectionStringState current = ConnectionStringState.FromCurrentConnection(new CurrentConnection(HELD_ISLAND, "conn-str"));
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(10);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(current, LKConnectionState.ConnConnected, HELD_ISLAND, NOW, nextAttempt, out string? _);

            // Assert
            Assert.IsFalse(shouldConnect);
        }

        [Test]
        public void SkipWhenNoStringReceived()
        {
            // Arrange: nothing pushed by the server yet — never connect, even disconnected and past backoff
            DateTime nextAttempt = NOW - TimeSpan.FromSeconds(10);

            // Act
            bool shouldConnect = ArchipelagoIslandRoom.ShouldAttemptConnection(ConnectionStringState.None(), LKConnectionState.ConnDisconnected, HELD_ISLAND, NOW, nextAttempt, out string? _);

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
        public void ConsumePendingBecomesCurrentKeepingTheStringAndIsland()
        {
            // The island rides along so a suppressed re-announcement still refreshes the cached token
            ConnectionStringState consumed =
                ConnectionStringState.FromPendingConnection(new PendingConnection(HELD_ISLAND, "conn-str")).Consume();

            Assert.IsTrue(consumed.IsCurrentConnection(out CurrentConnection current));
            Assert.AreEqual("conn-str", current.ConnectionString);
            Assert.AreEqual(HELD_ISLAND, current.IslandId);
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
                ConnectionStringState.FromPendingConnection(new PendingConnection(HELD_ISLAND, "conn-str")).Consume();

            ConnectionStringState reconsumed = current.Consume();

            Assert.IsTrue(reconsumed.IsCurrentConnection(out CurrentConnection currentConnection));
            Assert.AreEqual("conn-str", currentConnection.ConnectionString);
        }
    }
}
