using Arch.Core;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.Time.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.GliderProp;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

namespace DCL.CharacterMotion.Tests
{
    /// <summary>
    /// Regression tests for <see cref="GliderPropControllerSystem"/>.
    ///
    /// Covers the NullReferenceException reported in
    /// https://github.com/decentraland/unity-explorer/issues/8064
    /// where <c>glidingSettings.EnablePropPooling</c> was accessed on a null reference
    /// during world teardown, and a latent bug where <c>propPool!</c> was called with the
    /// null-forgiving operator even when the pool was never created.
    /// </summary>
    [TestFixture]
    public class GliderPropControllerSystemShould : UnitySystemTestBase<GliderPropControllerSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;
        private Transform poolRoot;
        private GliderPropView prefab;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            poolRoot = new GameObject("PoolRoot").transform;
            poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            poolsRegistry.RootContainerTransform().Returns(poolRoot);

            // Minimal GliderPropView prefab (no Animators/AudioSources required for cleanup path).
            prefab = new GameObject("GliderPropPrefab").AddComponent<GliderPropView>();

            // GliderPropControllerSystem.Initialize() calls World.CachePhysicsTick() which
            // requires a PhysicsTickComponent entity to be present in the world.
            new UpdatePhysicsTickSystem(world);
        }

        protected override void OnTearDown()
        {
            if (poolRoot != null)
                UnityObjectUtils.SafeDestroyGameObject(poolRoot.gameObject);
            if (prefab != null)
                UnityObjectUtils.SafeDestroyGameObject(prefab.gameObject);
        }

        /// <summary>
        /// When <c>glidingSettings</c> is null (edge-case: system invoked during world teardown
        /// after the owning container has been partially disposed), <c>CleanUpDestroyedAvatarsProp</c>
        /// must fall back to <c>Object.Destroy</c> without throwing.
        /// </summary>
        [Test]
        public void DestroyViewWhenGlidingSettingsIsNull()
        {
            // System constructed with null glidingSettings to simulate the teardown race.
            system = new GliderPropControllerSystem(world, null!, prefab, poolsRegistry);
            system.Initialize();

            var view = new GameObject("View").AddComponent<GliderPropView>();
            world.Create(new GliderProp { View = view }, new DeleteEntityIntention());

            Assert.DoesNotThrow(() => system.Update(0),
                "Update must not throw when glidingSettings is null");
        }

        /// <summary>
        /// When <c>EnablePropPooling</c> is false (the default), cleanup must call
        /// <c>Object.Destroy</c>. Previously the code used <c>propPool!</c> which would
        /// NRE if <c>propPool</c> was null in non-Editor builds.
        /// </summary>
        [Test]
        public void DestroyViewWhenPoolingIsDisabled()
        {
            // Default GlidingSettings has EnablePropPooling = false.
            var glidingSettings = new CharacterMotionSettings.GlidingSettings();

            system = new GliderPropControllerSystem(world, glidingSettings, prefab, poolsRegistry);
            system.Initialize();

            var view = new GameObject("View_NoPool").AddComponent<GliderPropView>();
            world.Create(new GliderProp { View = view }, new DeleteEntityIntention());

            Assert.DoesNotThrow(() => system.Update(0),
                "Update must not throw when pooling is disabled");
        }
    }
}
