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

namespace DCL.Tests
{
    public class ParticleSystemSystemShould : UnitySystemTestBase<ParticleSystemLifecycleSystem>
    {
        private ISceneStateProvider sceneStateProvider;
        private IComponentPool<UnityEngine.ParticleSystem> pool;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            pool = Substitute.For<IComponentPool<UnityEngine.ParticleSystem>>();

            var go = new GameObject("TestParticleSystem");
            var ps = go.AddComponent<UnityEngine.ParticleSystem>();
            pool.Get().Returns(ps);

            system = new ParticleSystemLifecycleSystem(world, sceneStateProvider, pool);
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

            var go2 = new GameObject("TestPS2");
            var ps2 = go2.AddComponent<UnityEngine.ParticleSystem>();
            pool.Get().Returns(ps2);

            system.Update(0);

            system.FinalizeComponents(default);

            pool.Received(2).Release(Arg.Any<UnityEngine.ParticleSystem>());
        }

        [Test]
        public void MarkPBDirtyAfterCreation()
        {
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);

            ref var pb = ref world.Get<PBParticleSystem>(entity);
            Assert.IsTrue(pb.IsDirty);
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
            var go = new GameObject("TestPS");
            var ps = go.AddComponent<UnityEngine.ParticleSystem>();
            var component = new ParticleSystemComponent(ps, go);
            component.LastRestartCount = 0;

            var pb = new PBParticleSystem { RestartCount = 1, IsDirty = true };

            Entity entity = world.Create(pb, component);
            system.Update(0);

            // After update, LastRestartCount should match RestartCount
            ref var updatedComponent = ref world.Get<ParticleSystemComponent>(entity);
            Assert.AreEqual(1u, updatedComponent.LastRestartCount);

            Object.DestroyImmediate(go);
        }
    }
}
