using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Simulation.World;
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

            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Velocity should have gained a negative X component (toward anchor)
            Assert.That(buffer[0].vel.x, Is.LessThan(originalVelX));
        }

        [Test]
        public void LeaveYAxisFree()
        {
            SetSpringStrength(50f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x, ANCHOR_POS.y + 5f, ANCHOR_POS.z),
                    vel: new Vector3(0f, 2f, 0f),
                    anchor: anchor),
            };

            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            // Y velocity should be unchanged — spring only acts on XZ
            Assert.That(buffer[0].vel.y, Is.EqualTo(2f).Within(1e-6f));
        }

        [Test]
        public void SkipUnanchoredParticles()
        {
            SetSpringStrength(50f);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(100f, 0f, 100f),
                    vel: Vector3.zero,
                    anchor: ChatReactionsParticle.ANCHOR_NONE),
            };

            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            Assert.That(buffer[0].vel, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void NoOpWhenStrengthIsZero()
        {
            SetSpringStrength(0f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 5f, ANCHOR_POS.y, ANCHOR_POS.z),
                    vel: Vector3.zero,
                    anchor: anchor),
            };

            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

            Assert.That(buffer[0].vel, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SkipInactiveAnchors()
        {
            SetSpringStrength(50f);
            byte anchor = anchorTable.Allocate("wallet_a", ANCHOR_POS);

            // Deactivate the anchor by refreshing with a provider that returns null
            anchorTable.Refresh(new NullAvatarPosition());

            var buffer = new[]
            {
                MakeParticle(
                    pos: new Vector3(ANCHOR_POS.x + 5f, ANCHOR_POS.y, ANCHOR_POS.z),
                    vel: Vector3.zero,
                    anchor: anchor),
            };

            var force = new AnchorSpringForce(anchorTable, config);
            force.Apply(buffer, 1, DT);

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

        private class NullAvatarPosition : IAvatarReactionPosition
        {
            public Vector3? GetLocalPlayerHeadPosition() => null;
            public Vector3? GetHeadPosition(string walletId) => null;
            public System.Collections.Generic.List<Vector3> GetAllNearbyHeadPositions() => new ();
            public int LastNearbyCount => 0;
            public int GetNearbyAvatarCount() => 0;
        }
    }
}
