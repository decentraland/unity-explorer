using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Simulation.World;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class AnchorSpringForceShould
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
            SetSpringStrength(50f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            // Particle offset from anchor on X axis
            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 1f, ANCHOR_POS.y, ANCHOR_POS.z),
                    anchor: anchor),
            };

            float originalVelX = buffer[0].vel.x;

            // Act
            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert — velocity should have gained a negative X component (toward anchor)
            Assert.That(buffer[0].vel.x, Is.LessThan(originalVelX));
        }

        [Test]
        public void LeaveYAxisFree()
        {
            // Arrange
            SetSpringStrength(50f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x, ANCHOR_POS.y + 5f, ANCHOR_POS.z),
                    vel: new Vector3(0f, 2f, 0f),
                    anchor: anchor),
            };

            // Act
            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert — Y velocity should be unchanged since spring only acts on XZ
            Assert.That(buffer[0].vel.y, Is.EqualTo(2f).Within(1e-6f));
        }

        [Test]
        public void SkipUnanchoredParticles()
        {
            // Arrange
            SetSpringStrength(50f);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(100f, 0f, 100f),
                    vel: Vector3.zero,
                    anchor: ChatReactionsParticle.ANCHOR_NONE),
            };

            // Act
            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert
            Assert.That(buffer[0].vel, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void NoOpWhenStrengthIsZero()
        {
            // Arrange
            SetSpringStrength(0f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 5f, ANCHOR_POS.y, ANCHOR_POS.z),
                    vel: Vector3.zero,
                    anchor: anchor),
            };

            // Act
            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert
            Assert.That(buffer[0].vel, Is.EqualTo(Vector3.zero));
        }

        // Verifies that particles bound to a deactivated anchor are not affected by the spring.
        [Test]
        public void SkipInactiveAnchors()
        {
            // Arrange
            SetSpringStrength(50f);
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

            // Act
            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Assert
            Assert.That(buffer[0].vel, Is.EqualTo(Vector3.zero));
        }

        // ── Helpers ─────────────────────────────────────────

        private void SetSpringStrength(float strength)
        {
            // Use SerializedObject to set the private backing field
            // The config uses [field: SerializeField] which creates backing fields named
            // <PropertyName>k__BackingField. Use reflection for test setup.
            var so = new UnityEditor.SerializedObject(config);
            var prop = so.FindProperty("<SpringStrength>k__BackingField");
            prop.floatValue = strength;
            so.ApplyModifiedPropertiesWithoutUndo();
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
