using System;
using DCL.Chat.ChatReactions.Configs;
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
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            reactor.TriggerLocalBurst(EMOJI_INDEX);

            spawner.Received(1).TriggerAnchoredReactionLocalPlayer(HEAD_POS, EMOJI_INDEX, worldConfig.BurstCount);
        }

        [Test]
        public void TriggerRemoteBurstWithCorrectParameters()
        {
            SetupRemoteHeadPosition("wallet_a", HEAD_POS);
            var reactor = CreateReactor();

            reactor.TriggerRemoteBurst("wallet_a", EMOJI_INDEX, 5);

            spawner.Received(1).TriggerAnchoredReaction(HEAD_POS, "wallet_a", EMOJI_INDEX, 5);
        }

        [Test]
        public void BeginStreamWithCorrectParameters()
        {
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            reactor.BeginStream();

            spawner.Received(1).BeginStream(
                Arg.Any<Func<Vector3?>>(),
                -1,
                AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        [Test]
        public void EndStreamAlways()
        {
            var reactor = CreateReactorWithNullAvatar();

            reactor.EndStream();

            spawner.Received(1).EndStream();
        }

        // ── SyncStreamState ──────────────────────────────────────────

        [Test]
        public void SyncStreamStateBeginWhenStreamingAndEnabled()
        {
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            reactor.SyncStreamState(true);

            spawner.Received(1).BeginStream(
                Arg.Any<Func<Vector3?>>(),
                -1,
                AvatarAnchorTable.LOCAL_PLAYER_ID);
        }

        [Test]
        public void SyncStreamStateEndWhenStreamingButDisabled()
        {
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();
            reactor.WorldReactionsEnabled = false;

            reactor.SyncStreamState(true);

            spawner.DidNotReceiveWithAnyArgs().BeginStream(default, default, default);
            spawner.Received(1).EndStream();
        }

        [Test]
        public void SyncStreamStateEndWhenNotStreaming()
        {
            SetupLocalHeadPosition(HEAD_POS);
            var reactor = CreateReactor();

            reactor.SyncStreamState(false);

            spawner.DidNotReceiveWithAnyArgs().BeginStream(default, default, default);
            spawner.Received(1).EndStream();
        }

        [Test]
        public void SyncStreamStateNoOpWhenAvatarPositionIsNull()
        {
            var reactor = CreateReactorWithNullAvatar();

            reactor.SyncStreamState(true);

            spawner.DidNotReceiveWithAnyArgs().BeginStream(default, default, default);
            spawner.DidNotReceiveWithAnyArgs().EndStream();
        }
    }
}
