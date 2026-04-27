using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Simulation.World;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class LocalPlayerWorldReactorShould
    {
        private IWorldReactionSpawner spawner;
        private IAvatarReactionPosition avatarPosition;
        private ChatReactionsWorldLaneConfig worldConfig;

        private static readonly Vector3 HEAD_POS = new (10f, 2f, 5f);
        private const int EMOJI_INDEX = 3;

        [SetUp]
        public void SetUp()
        {
            spawner = Substitute.For<IWorldReactionSpawner>();
            avatarPosition = Substitute.For<IAvatarReactionPosition>();
            worldConfig = ScriptableObject.CreateInstance<ChatReactionsWorldLaneConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(worldConfig);
        }

        private LocalPlayerWorldReactor CreateReactor(IAvatarReactionPosition? avatar = null) =>
            new (spawner, worldConfig, avatar ?? avatarPosition);

        private LocalPlayerWorldReactor CreateReactorWithNullAvatar() =>
            new (spawner, worldConfig, null);

        private void SetupLocalHeadPosition(Vector3? pos)
        {
            avatarPosition.GetLocalPlayerHeadPosition().Returns(pos);
        }

        private void SetupRemoteHeadPosition(string walletId, Vector3? pos)
        {
            avatarPosition.GetHeadPosition(walletId).Returns(pos);
        }

        // ── Parameter forwarding ─────────────────────────────────────

        [Test]
        public void TriggerLocalBurstWithCorrectParameters()
        {
            // Arrange
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            // Act
            reactor.TriggerLocalBurst(EMOJI_INDEX);

            // Assert
            spawner.Received(1).TriggerAnchoredReactionLocalPlayer(HEAD_POS, EMOJI_INDEX, worldConfig.BurstCount);
        }

        [Test]
        public void TriggerRemoteBurstWithCorrectParameters()
        {
            // Arrange
            SetupRemoteHeadPosition("wallet_a", HEAD_POS);
            var reactor = CreateReactor();

            // Act
            reactor.TriggerRemoteBurst("wallet_a", EMOJI_INDEX, 5);

            // Assert
            spawner.Received(1).TriggerAnchoredReaction(HEAD_POS, "wallet_a", EMOJI_INDEX, 5);
        }

        [Test]
        public void BeginStreamWithCorrectParameters()
        {
            // Arrange
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            // Act
            reactor.BeginStream();

            // Assert
            spawner.Received(1).BeginStream(
                Arg.Any<Func<Vector3?>>(),
                -1,
                AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        [Test]
        public void EndStreamAlways()
        {
            // Arrange
            var reactor = CreateReactorWithNullAvatar();

            // Act
            reactor.EndStream();

            // Assert
            spawner.Received(1).EndStream();
        }

        // ── SyncStreamState ──────────────────────────────────────────

        [Test]
        public void SyncStreamStateBeginWhenStreamingAndEnabled()
        {
            // Arrange
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            // Act
            reactor.SyncStreamState(true);

            // Assert
            spawner.Received(1).BeginStream(
                Arg.Any<Func<Vector3?>>(),
                -1,
                AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        [Test]
        public void SyncStreamStateEndWhenStreamingButDisabled()
        {
            // Arrange
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();
            reactor.WorldReactionsEnabled = false;

            // Act
            reactor.SyncStreamState(true);

            // Assert
            spawner.DidNotReceiveWithAnyArgs().BeginStream(default, default, default);
            spawner.Received(1).EndStream();
        }

        [Test]
        public void SyncStreamStateEndWhenNotStreaming()
        {
            // Arrange
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            // Act
            reactor.SyncStreamState(false);

            // Assert
            spawner.DidNotReceiveWithAnyArgs().BeginStream(default, default, default);
            spawner.Received(1).EndStream();
        }

        // When no avatar position provider exists, sync should silently do nothing.
        [Test]
        public void SyncStreamStateNoOpWhenAvatarPositionIsNull()
        {
            // Arrange
            var reactor = CreateReactorWithNullAvatar();

            // Act
            reactor.SyncStreamState(true);

            // Assert
            spawner.DidNotReceiveWithAnyArgs().BeginStream(default, default, default);
            spawner.DidNotReceiveWithAnyArgs().EndStream();
        }
    }
}
