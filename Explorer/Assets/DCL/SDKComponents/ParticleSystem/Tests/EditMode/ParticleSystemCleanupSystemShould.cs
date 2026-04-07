using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.ParticleSystem;
using DCL.SDKComponents.ParticleSystem.Systems;
using DCL.WebRequests;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Entity = Arch.Core.Entity;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.ParticleSystem.Tests
{
    public class ParticleSystemCleanupSystemShould : UnitySystemTestBase<ParticleSystemCleanupSystem>
    {
        private IComponentPool<UnityEngine.ParticleSystem> pool;
        private IObjectPool<Material> materialPool;

        [SetUp]
        public void SetUp()
        {
            pool = Substitute.For<IComponentPool<UnityEngine.ParticleSystem>>();
            materialPool = Substitute.For<IObjectPool<Material>>();

            system = new ParticleSystemCleanupSystem(world, pool, materialPool);
        }

        [TearDown]
        public void TearDown()
        {
            var query = new QueryDescription().WithAll<ParticleSystemComponent>();
            world.Query(query, (ref ParticleSystemComponent c) =>
            {
                if (c.HostGameObject != null)
                    Object.DestroyImmediate(c.HostGameObject);
            });
        }

        [Test]
        public void ReleaseAndRemoveComponentOnRemoval()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();

            Entity entity = world.Create(new ParticleSystemComponent(testParticleSystem, testGameObject));
            system.Update(0);

            pool.Received(1).Release(Arg.Any<UnityEngine.ParticleSystem>());
            Assert.IsFalse(world.Has<ParticleSystemComponent>(entity));
        }

        [Test]
        public void ReleaseOnEntityDestruction()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();

            Entity entity = world.Create(
                new ParticleSystemComponent(testParticleSystem, testGameObject),
                new DeleteEntityIntention());

            system.Update(0);

            pool.Received(1).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void FinalizeReleaseAllInstances()
        {
            var go1 = new GameObject("TestPS1");
            var ps1 = go1.AddComponent<UnityEngine.ParticleSystem>();
            var go2 = new GameObject("TestPS2");
            var ps2 = go2.AddComponent<UnityEngine.ParticleSystem>();

            world.Create(new ParticleSystemComponent(ps1, go1));
            world.Create(new ParticleSystemComponent(ps2, go2));

            system.FinalizeComponents(default);

            pool.Received(2).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void NotCleanUpWhenPBComponentStillPresent()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();

            Entity entity = world.Create(
                new PBParticleSystem(),
                new ParticleSystemComponent(testParticleSystem, testGameObject));

            system.Update(0);

            Assert.IsTrue(world.Has<ParticleSystemComponent>(entity));
            pool.DidNotReceive().Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void NotDoubleReleaseOnSecondUpdateAfterRemoval()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();

            Entity entity = world.Create(new ParticleSystemComponent(testParticleSystem, testGameObject));

            system.Update(0);
            pool.Received(1).Release(Arg.Any<UnityEngine.ParticleSystem>());

            system.Update(0);
            pool.Received(1).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void ReleaseMaterialOnCleanup()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            component.ParticleMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            world.Create(component);
            system.Update(0);

            materialPool.Received(1).Release(Arg.Any<Material>());
        }

        [Test]
        public void CancelTexturePromiseOnComponentRemoval()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            var intention = new GetTextureIntention("test-url", "test-hash",
                TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "test");

            component.TexturePromise = TexturePromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            var cancellationToken = component.TexturePromise.Value.LoadingIntention.CancellationTokenSource.Token;

            world.Create(component);
            system.Update(0);

            Assert.IsTrue(cancellationToken.IsCancellationRequested);
        }

        [Test]
        public void CancelTexturePromiseOnEntityDestruction()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            var intention = new GetTextureIntention("test-url", "test-hash",
                TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "test");

            component.TexturePromise = TexturePromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            var cancellationToken = component.TexturePromise.Value.LoadingIntention.CancellationTokenSource.Token;

            world.Create(component, new DeleteEntityIntention());
            system.Update(0);

            Assert.IsTrue(cancellationToken.IsCancellationRequested);
        }

        [Test]
        public void CancelTexturePromiseOnFinalize()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            var intention = new GetTextureIntention("test-url", "test-hash",
                TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "test");

            component.TexturePromise = TexturePromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            var cancellationToken = component.TexturePromise.Value.LoadingIntention.CancellationTokenSource.Token;

            world.Create(component);
            system.FinalizeComponents(default);

            Assert.IsTrue(cancellationToken.IsCancellationRequested);
        }

        [Test]
        public void NotFailWhenNoTexturePromisePresent()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(component);

            Assert.DoesNotThrow(() => system.Update(0));
            pool.Received(1).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void DereferenceResolvedTextureOnCleanup()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            // Simulate post-consume state: TexturePromise is null, SourceTextureData holds the resolved texture
            var texData = new TextureData(Texture2D.grayTexture);
            texData.AddReference();
            component.SourceTextureData = texData;

            Assert.AreEqual(1, texData.referenceCount);

            world.Create(component);
            system.Update(0);

            Assert.AreEqual(0, texData.referenceCount);
        }
    }
}
