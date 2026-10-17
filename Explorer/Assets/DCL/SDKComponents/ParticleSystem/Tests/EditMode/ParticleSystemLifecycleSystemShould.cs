using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.ParticleSystem;
using DCL.SDKComponents.ParticleSystem.Systems;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Entity = Arch.Core.Entity;

namespace DCL.ParticleSystem.Tests
{
    public class ParticleSystemLifecycleSystemShould : UnitySystemTestBase<ParticleSystemLifecycleSystem>
    {
        private ISceneStateProvider sceneStateProvider;
        private IComponentPool<UnityEngine.ParticleSystem> pool;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            pool = Substitute.For<IComponentPool<UnityEngine.ParticleSystem>>();

            var testGameObject = new GameObject("TestParticleSystem");
            var testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            pool.Get().Returns(testParticleSystem);

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
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());

            system.Update(0);

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
        public void MarkPBDirtyAfterCreation()
        {
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);

            ref var particleSystemData = ref world.Get<PBParticleSystem>(entity);
            Assert.IsTrue(particleSystemData.IsDirty);
        }

        [Test]
        public void NotCreateDuplicateComponentOnSecondUpdate()
        {
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);
            system.Update(0);

            Assert.IsTrue(world.Has<ParticleSystemComponent>(entity));
            pool.Received(1).Get();
        }
    }
}
