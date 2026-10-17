using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Simulation.World;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class AnchorFollowForceShould
    {
        private AvatarAnchorTable anchorTable;
        private ChatReactionsWorldLaneConfig config;

        private const float DT = 0.016f;
        private static readonly Vector3 ANCHOR_POS = new (10f, 5f, 10f);

        [SetUp]
        public void SetUp()
        {
            anchorTable = new AvatarAnchorTable();
            config = ScriptableObject.CreateInstance<ChatReactionsWorldLaneConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void PullParticleTowardAnchorOnXZ()
        {
            // Arrange
            SetFollowRate(5f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 1f, ANCHOR_POS.y, ANCHOR_POS.z),
                    anchor: anchor),
            };

            float originalPosX = buffer[0].pos.x;

            // Act
            var force = new AnchorFollowForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert — position should have moved toward anchor (decreased X)
            Assert.That(buffer[0].pos.x, Is.LessThan(originalPosX));
            Assert.That(buffer[0].pos.x, Is.GreaterThan(ANCHOR_POS.x),
                "Must not overshoot the anchor");
        }

        [Test]
        public void LeaveYAxisFree()
        {
            // Arrange
            SetFollowRate(5f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x, ANCHOR_POS.y + 5f, ANCHOR_POS.z),
                    vel: new Vector3(0f, 2f, 0f),
                    anchor: anchor),
            };

            float originalPosY = buffer[0].pos.y;

            // Act
            var force = new AnchorFollowForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert — Y position and velocity should be unchanged
            Assert.That(buffer[0].pos.y, Is.EqualTo(originalPosY).Within(1e-6f));
            Assert.That(buffer[0].vel.y, Is.EqualTo(2f).Within(1e-6f));
        }

        [Test]
        public void SkipUnanchoredParticles()
        {
            // Arrange
            SetFollowRate(5f);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(100f, 0f, 100f),
                    vel: Vector3.zero,
                    anchor: ChatReactionsParticle.ANCHOR_NONE),
            };

            Vector3 originalPos = buffer[0].pos;

            // Act
            var force = new AnchorFollowForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert
            Assert.That(buffer[0].pos, Is.EqualTo(originalPos));
        }

        [Test]
        public void NoOpWhenRateIsZero()
        {
            // Arrange
            SetFollowRate(0f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 5f, ANCHOR_POS.y, ANCHOR_POS.z),
                    vel: Vector3.zero,
                    anchor: anchor),
            };

            Vector3 originalPos = buffer[0].pos;

            // Act
            var force = new AnchorFollowForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert
            Assert.That(buffer[0].pos, Is.EqualTo(originalPos));
        }

        // Verifies that particles bound to a deactivated anchor are not affected.
        [Test]
        public void SkipInactiveAnchors()
        {
            // Arrange
            SetFollowRate(5f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            // Deactivate the anchor by refreshing with a provider that returns null for all wallets
            anchorTable.Refresh(Substitute.For<IAvatarReactionPosition>());

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 5f, ANCHOR_POS.y, ANCHOR_POS.z),
                    vel: Vector3.zero,
                    anchor: anchor),
            };

            Vector3 originalPos = buffer[0].pos;

            // Act
            var force = new AnchorFollowForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert
            Assert.That(buffer[0].pos, Is.EqualTo(originalPos));
        }

        [Test]
        public void DoesNotOvershootTarget()
        {
            // Arrange — particle very close to anchor
            SetFollowRate(15f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 0.001f, ANCHOR_POS.y, ANCHOR_POS.z + 0.001f),
                    anchor: anchor),
            };

            // Act — apply many times to stress-test convergence
            var force = new AnchorFollowForce(anchorTable, config);

            for (int i = 0; i < 100; i++)
                force.Apply(buffer, 1, DT);

            // Assert — particle must stay on the same side or reach anchor, never cross it
            Assert.That(buffer[0].pos.x, Is.GreaterThanOrEqualTo(ANCHOR_POS.x - 1e-6f));
            Assert.That(buffer[0].pos.z, Is.GreaterThanOrEqualTo(ANCHOR_POS.z - 1e-6f));
        }

        // ── Helpers ─────────────────────────────────────────

        private void SetFollowRate(float rate)
        {
            config.FollowRate = rate;
        }

        private static ChatReactionsParticle MakeParticle(
            Vector3 pos = default, Vector3 vel = default,
            byte anchor = ChatReactionsParticle.ANCHOR_NONE,
            float age = 0f, float lifetime = 2f) =>
            new ()
            {
                pos = pos,
                vel = vel,
                age = age,
                lifetime = lifetime,
                alive = 1,
                anchorIndex = anchor,
            };

    }
}
