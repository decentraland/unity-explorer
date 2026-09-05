using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Simulation.World;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ChatReactionWorldSimulationScalingShould
    {
        private ChatReactionsConfig config;
        private ChatReactionWorldSimulation sim;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ChatReactionsConfig>();

            var worldLane = ScriptableObject.CreateInstance<ChatReactionsWorldLaneConfig>();
            worldLane.MaxParticles = 100;
            worldLane.MaxParticlesPerAvatar = 15;
            worldLane.BurstCount = 1;
            worldLane.LifetimeRange = new Vector2(10f, 10f);
            worldLane.SpeedRange = new Vector2(1f, 1f);
            worldLane.SizeRange = new Vector2(0.1f, 0.1f);
            config.WorldLane = worldLane;

            // Provide a dummy material so ChatReactionMaterialFactory.CreateRuntimeMaterial succeeds.
            config.EmojiMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

            config.DynamicScalingEnabled = false;
            config.WorldPoolTargetUtilization = 0.7f;

            sim = new ChatReactionWorldSimulation(config);
        }

        [TearDown]
        public void TearDown()
        {
            sim.Dispose();
            Object.DestroyImmediate(config.WorldLane);
            Object.DestroyImmediate(config.EmojiMaterial);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void UseStaticCapWhenDynamicScalingDisabled()
        {
            config.DynamicScalingEnabled = false;

            for (int i = 0; i < 20; i++)
                sim.TriggerAnchoredReaction(Vector3.zero, "wallet_a", 0, 1);

            Assert.That(sim.AliveCount, Is.EqualTo(15));
            Assert.That(sim.CappedThisFrame, Is.GreaterThan(0));
        }

        [Test]
        public void UseDynamicCapWithSingleAvatarWhenLowerThanStatic()
        {
            // 1 avatar, pool=100, utilization=0.1 → budget=10, dynamic=10
            // min(static=15, dynamic=10) = 10 → dynamic cap limits
            config.DynamicScalingEnabled = true;
            config.WorldPoolTargetUtilization = 0.1f;

            for (int i = 0; i < 20; i++)
                sim.TriggerAnchoredReaction(Vector3.zero, "wallet_a", 0, 1);

            Assert.That(sim.AliveCount, Is.EqualTo(10));
            Assert.That(sim.CappedThisFrame, Is.GreaterThan(0));
        }

        [Test]
        public void ReduceCapWithManyAvatars()
        {
            config.DynamicScalingEnabled = true;

            for (int i = 0; i < 10; i++)
                sim.TriggerAnchoredReaction(Vector3.zero, $"wallet_{i}", 0, 1);

            sim.Tick(0.016f);

            for (int i = 0; i < 15; i++)
                sim.TriggerAnchoredReaction(Vector3.zero, "wallet_0", 0, 1);

            // 10 wallets × 1 particle + wallet_0 capped at dynamic 7 = 16 max
            Assert.That(sim.AliveCount, Is.LessThanOrEqualTo(16));
            Assert.That(sim.CappedThisFrame, Is.GreaterThan(0));
        }

        [Test]
        public void DistributeEquallyAcrossAvatars()
        {
            config.DynamicScalingEnabled = true;

            for (int i = 0; i < 7; i++)
                sim.TriggerAnchoredReaction(Vector3.zero, $"wallet_{i}", 0, 1);

            sim.Tick(0.016f);

            for (int i = 0; i < 14; i++)
                sim.TriggerAnchoredReaction(Vector3.zero, "wallet_0", 0, 1);

            // 7 wallets × 1 particle + wallet_0 capped at dynamic 10 = 16 max
            Assert.That(sim.AliveCount, Is.LessThanOrEqualTo(16));
        }

        [Test]
        public void NotCrashWhenFirstAnchorIsAllocated()
        {
            // The zero-anchor branch in ComputeEffectiveMaxPerAvatar is a safety guard.
            // TriggerAnchoredReaction calls Allocate() before the cap check, so
            // ActiveSlotCount is always >= 1 when the method runs. This test verifies
            // the first allocation works cleanly with dynamic scaling enabled.
            config.DynamicScalingEnabled = true;

            sim.TriggerAnchoredReaction(Vector3.zero, "wallet_a", 0, 1);
            Assert.That(sim.AliveCount, Is.EqualTo(1));
        }

    }
}
