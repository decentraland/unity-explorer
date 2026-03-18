using System.Collections.Generic;
using Arch.Core;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.ParticleSystem;
using DCL.SDKComponents.ParticleSystem.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Tests
{
    public class ParticleSystemSystemShould : UnitySystemTestBase<ParticleSystemLifecycleSystem>
    {
        private ISceneStateProvider sceneStateProvider;
        private IComponentPool<UnityEngine.ParticleSystem> pool;
        private IObjectPool<Material> materialPool;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            pool = Substitute.For<IComponentPool<UnityEngine.ParticleSystem>>();
            materialPool = Substitute.For<IObjectPool<Material>>();

            var testGameObject = new GameObject("TestParticleSystem");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            pool.Get().Returns(testParticleSystem);

            system = new ParticleSystemLifecycleSystem(world, sceneStateProvider, pool, materialPool);
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
        public void CreateParticleSystemComponentOnFirstUpdate()
        {
            // Arrange
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<ParticleSystemComponent>(entity));
            pool.Received(1).Get();
        }

        [Test]
        public void NotCreateWhenSceneIsNotCurrent()
        {
            sceneStateProvider.IsCurrent.Returns(false);

            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);

            Assert.IsFalse(world.Has<ParticleSystemComponent>(entity));
        }

        [Test]
        public void ReleaseOnComponentRemoval()
        {
            // Arrange — create entity and run lifecycle
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);
            Assert.IsTrue(world.Has<ParticleSystemComponent>(entity));

            // Remove the SDK component (simulates component removal from scene)
            world.Remove<PBParticleSystem>(entity);
            system.Update(0);

            pool.Received(1).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void ReleaseOnEntityDestruction()
        {
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);

            world.Add<DeleteEntityIntention>(entity, new DeleteEntityIntention());
            system.Update(0);

            pool.Received(1).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void FinalizeReleaseAllInstances()
        {
            Entity e1 = world.Create(new PBParticleSystem(), new TransformComponent());
            Entity e2 = world.Create(new PBParticleSystem(), new TransformComponent());

            var secondGameObject = new GameObject("TestPS2");
            var secondParticleSystem = secondGameObject.AddComponent<UnityEngine.ParticleSystem>();
            pool.Get().Returns(secondParticleSystem);

            system.Update(0);

            system.FinalizeComponents(default);

            pool.Received(2).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void MarkPBDirtyAfterCreation()
        {
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);

            ref var particleSystemData = ref world.Get<PBParticleSystem>(entity);
            Assert.IsTrue(particleSystemData.IsDirty);
        }
    }

    public class ParticleSystemPlaybackSystemShould : UnitySystemTestBase<ParticleSystemPlaybackSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new ParticleSystemPlaybackSystem(world);
        }

        [Test]
        public void TriggerRestartOnRestartCountIncrement()
        {
            var testGameObject = new GameObject("TestPS");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            component.LastRestartCount = 0;

            var particleSystemData = new PBParticleSystem { RestartCount = 1, IsDirty = true };

            Entity entity = world.Create(particleSystemData, component);
            system.Update(0);

            // After update, LastRestartCount should match RestartCount
            ref var updatedComponent = ref world.Get<ParticleSystemComponent>(entity);
            Assert.AreEqual(1u, updatedComponent.LastRestartCount);

            Object.DestroyImmediate(testGameObject);
        }
    }
}
