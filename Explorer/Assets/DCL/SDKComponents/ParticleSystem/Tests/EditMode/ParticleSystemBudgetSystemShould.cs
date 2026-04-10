using DCL.DebugUtilities.UIBindings;
using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem;
using DCL.SDKComponents.ParticleSystem.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;

namespace DCL.ParticleSystem.Tests
{
    public class ParticleSystemBudgetSystemShould : UnitySystemTestBase<ParticleSystemBudgetSystem>
    {
        private GameObject testGameObject;
        private UnityEngine.ParticleSystem testParticleSystem;
        private ElementBinding<string> particleCountBinding;
        private DebugWidgetVisibilityBinding visibilityBinding;
        private ParticleSystemPlugin.ParticleSystemPluginSettings settings;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject("TestPS");
            testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            particleCountBinding = new ElementBinding<string>(string.Empty);
            visibilityBinding = new DebugWidgetVisibilityBinding(true);

            settings = new ParticleSystemPlugin.ParticleSystemPluginSettings();

            system = new ParticleSystemBudgetSystem(world, settings, particleCountBinding, visibilityBinding);
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        [Test]
        public void ApplyFullRateWhenUnderBudget()
        {
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { Rate = 20f };

            world.Create(pb, component);
            system.Update(0);

            // Under budget: multiplier = 1, so rateOverTimeMultiplier = 1 * 20
            Assert.AreEqual(20f, testParticleSystem.emission.rateOverTimeMultiplier);
        }

        [Test]
        public void UseDefaultRateWhenNotSet()
        {
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem();

            world.Create(pb, component);
            system.Update(0);

            // Under budget with default rate: multiplier = 1, rateOverTimeMultiplier = 1 * 10
            Assert.AreEqual(10f, testParticleSystem.emission.rateOverTimeMultiplier);
        }
    }
}
