using Arch.Core;
using DCL.DebugUtilities.UIBindings;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World;
using DCL.SDKComponents.ParticleSystem;
using DCL.SDKComponents.ParticleSystem.Components;
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
        public void ReleaseOnComponentRemoval()
        {
            Entity entity = world.Create(new PBParticleSystem { Rate = 10 }, new TransformComponent());
            system.Update(0);
            Assert.IsTrue(world.Has<ParticleSystemComponent>(entity));

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

    public class PBParticleSystemDefaultsShould
    {
        [Test]
        public void ReturnDefaultRateWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(10f, pb.GetRate());
        }

        [Test]
        public void ReturnExplicitRateWhenSet()
        {
            var pb = new PBParticleSystem { Rate = 50f };
            Assert.AreEqual(50f, pb.GetRate());
        }

        [Test]
        public void ReturnDefaultMaxParticlesWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(1000u, pb.GetMaxParticles());
        }

        [Test]
        public void ReturnExplicitMaxParticlesWhenSet()
        {
            var pb = new PBParticleSystem { MaxParticles = 500 };
            Assert.AreEqual(500u, pb.GetMaxParticles());
        }

        [Test]
        public void ReturnDefaultLifetimeWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(5f, pb.GetLifetime());
        }

        [Test]
        public void ReturnExplicitLifetimeWhenSet()
        {
            var pb = new PBParticleSystem { Lifetime = 3f };
            Assert.AreEqual(3f, pb.GetLifetime());
        }

        [Test]
        public void ReturnDefaultGravityWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(0f, pb.GetGravity());
        }

        [Test]
        public void ReturnExplicitGravityWhenSet()
        {
            var pb = new PBParticleSystem { Gravity = -9.8f };
            Assert.AreEqual(-9.8f, pb.GetGravity());
        }

        [Test]
        public void ReturnTrueForActiveWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsTrue(pb.GetActive());
        }

        [Test]
        public void ReturnFalseForActiveWhenExplicitlyFalse()
        {
            var pb = new PBParticleSystem { Active = false };
            Assert.IsFalse(pb.GetActive());
        }

        [Test]
        public void ReturnTrueForLoopWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsTrue(pb.GetLoop());
        }

        [Test]
        public void ReturnFalseForLoopWhenExplicitlyFalse()
        {
            var pb = new PBParticleSystem { Loop = false };
            Assert.IsFalse(pb.GetLoop());
        }

        [Test]
        public void ReturnFalseForPrewarmWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsFalse(pb.GetPrewarm());
        }

        [Test]
        public void ReturnTrueForPrewarmWhenExplicitlyTrue()
        {
            var pb = new PBParticleSystem { Prewarm = true };
            Assert.IsTrue(pb.GetPrewarm());
        }

        [Test]
        public void ReturnFalseForFaceTravelDirectionWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.IsFalse(pb.GetFaceTravelDirection());
        }

        [Test]
        public void ReturnDefaultBlendModeWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(PBParticleSystem.Types.BlendMode.PsbAlpha, pb.GetBlendMode());
        }

        [Test]
        public void ReturnExplicitBlendModeWhenSet()
        {
            var pb = new PBParticleSystem { BlendMode = PBParticleSystem.Types.BlendMode.PsbAdd };
            Assert.AreEqual(PBParticleSystem.Types.BlendMode.PsbAdd, pb.GetBlendMode());
        }

        [Test]
        public void ReturnDefaultSimulationSpaceWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(PBParticleSystem.Types.SimulationSpace.PssLocal, pb.GetSimulationSpace());
        }

        [Test]
        public void ReturnExplicitSimulationSpaceWhenSet()
        {
            var pb = new PBParticleSystem { SimulationSpace = PBParticleSystem.Types.SimulationSpace.PssWorld };
            Assert.AreEqual(PBParticleSystem.Types.SimulationSpace.PssWorld, pb.GetSimulationSpace());
        }

        [Test]
        public void ReturnDefaultPlaybackStateWhenNotSet()
        {
            var pb = new PBParticleSystem();
            Assert.AreEqual(PBParticleSystem.Types.PlaybackState.PsPlaying, pb.GetPlaybackState());
        }

        [Test]
        public void ReturnExplicitPlaybackStateWhenSet()
        {
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsPaused };
            Assert.AreEqual(PBParticleSystem.Types.PlaybackState.PsPaused, pb.GetPlaybackState());
        }

        [Test]
        public void ReturnDefaultSphereRadiusWhenNotSet()
        {
            var sphere = new PBParticleSystem.Types.Sphere();
            Assert.AreEqual(1f, sphere.GetRadius());
        }

        [Test]
        public void ReturnExplicitSphereRadiusWhenSet()
        {
            var sphere = new PBParticleSystem.Types.Sphere { Radius = 5f };
            Assert.AreEqual(5f, sphere.GetRadius());
        }

        [Test]
        public void ReturnDefaultConeAngleWhenNotSet()
        {
            var cone = new PBParticleSystem.Types.Cone();
            Assert.AreEqual(25f, cone.GetAngle());
        }

        [Test]
        public void ReturnDefaultConeRadiusWhenNotSet()
        {
            var cone = new PBParticleSystem.Types.Cone();
            Assert.AreEqual(1f, cone.GetRadius());
        }

        [Test]
        public void ReturnDefaultDampenWhenNotSet()
        {
            var limitVelocity = new PBParticleSystem.Types.LimitVelocity();
            Assert.AreEqual(1f, limitVelocity.GetDampen());
        }

        [Test]
        public void ReturnDefaultFramesPerSecondWhenNotSet()
        {
            var spriteSheet = new PBParticleSystem.Types.SpriteSheetAnimation();
            Assert.AreEqual(30f, spriteSheet.GetFramesPerSecond());
        }
    }

    public class ParticleSystemApplyPropertiesSystemShould : UnitySystemTestBase<ParticleSystemApplyPropertiesSystem>
    {
        private GameObject testGameObject;
        private UnityEngine.ParticleSystem testParticleSystem;
        private IObjectPool<Material> materialPool;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject("TestPS");
            testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();

            var sceneData = Substitute.For<ISceneData>();
            var partitionComponent = Substitute.For<ECS.Prioritization.Components.IPartitionComponent>();
            materialPool = Substitute.For<IObjectPool<Material>>();
            materialPool.Get().Returns(new Material(Shader.Find("Universal Render Pipeline/Lit")));

            system = new ParticleSystemApplyPropertiesSystem(world, sceneData, partitionComponent, materialPool);
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        [Test]
        public void ApplyDefaultValuesWhenFieldsNotSet()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var main = testParticleSystem.main;
            Assert.AreEqual(true, main.loop);
            Assert.AreEqual(5f, main.startLifetime.constant);
            Assert.AreEqual(1000, main.maxParticles);
            Assert.AreEqual(0f, main.gravityModifier.constant);
            Assert.AreEqual(ParticleSystemSimulationSpace.Local, main.simulationSpace);

            var emission = testParticleSystem.emission;
            Assert.IsTrue(emission.enabled);
            Assert.AreEqual(10f, emission.rateOverTime.constant);
        }

        [Test]
        public void ApplyExplicitValues()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                Rate = 25f,
                Lifetime = 3f,
                MaxParticles = 200,
                Gravity = -5f,
                Loop = false,
                SimulationSpace = PBParticleSystem.Types.SimulationSpace.PssWorld
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var main = testParticleSystem.main;
            Assert.AreEqual(false, main.loop);
            Assert.AreEqual(3f, main.startLifetime.constant);
            Assert.AreEqual(200, main.maxParticles);
            Assert.AreEqual(-5f, main.gravityModifier.constant);
            Assert.AreEqual(ParticleSystemSimulationSpace.World, main.simulationSpace);
            Assert.AreEqual(25f, testParticleSystem.emission.rateOverTime.constant);
        }

        [Test]
        public void DisableEmissionWhenActiveFalse()
        {
            var pb = new PBParticleSystem { IsDirty = true, Active = false };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsFalse(testParticleSystem.emission.enabled);
        }

        [Test]
        public void ApplyPointShapeByDefault()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.AreEqual(ParticleSystemShapeType.Sphere, testParticleSystem.shape.shapeType);
            Assert.That(testParticleSystem.shape.radius, Is.LessThan(0.001f));
        }

        [Test]
        public void ApplySphereShapeWithDefaultRadius()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                Sphere = new PBParticleSystem.Types.Sphere()
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.AreEqual(ParticleSystemShapeType.Sphere, testParticleSystem.shape.shapeType);
            Assert.AreEqual(1f, testParticleSystem.shape.radius);
        }

        [Test]
        public void ApplyConeShapeWithDefaultValues()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                Cone = new PBParticleSystem.Types.Cone()
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.AreEqual(ParticleSystemShapeType.Cone, testParticleSystem.shape.shapeType);
            Assert.AreEqual(25f, testParticleSystem.shape.angle);
            Assert.AreEqual(1f, testParticleSystem.shape.radius);
        }

        [Test]
        public void DisableSizeOverLifetimeWhenNull()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsFalse(testParticleSystem.sizeOverLifetime.enabled);
        }

        [Test]
        public void EnableSizeOverLifetimeWhenProvided()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                SizeOverTime = new PBParticleSystem.Types.FloatRange { Start = 0.5f, End = 2f }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.sizeOverLifetime.enabled);
        }

        [Test]
        public void DisableRotationOverLifetimeWhenNull()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsFalse(testParticleSystem.rotationOverLifetime.enabled);
        }

        [Test]
        public void DisableForceOverLifetimeWhenNull()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsFalse(testParticleSystem.forceOverLifetime.enabled);
        }

        [Test]
        public void SkipUpdateWhenNotDirty()
        {
            var pb = new PBParticleSystem { IsDirty = false, Rate = 99f };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            // Rate should not be applied since IsDirty is false
            Assert.AreNotEqual(99f, testParticleSystem.emission.rateOverTime.constant);
        }
    }

    public class ParticleSystemPlaybackSystemShould : UnitySystemTestBase<ParticleSystemPlaybackSystem>
    {
        private GameObject testGameObject;
        private UnityEngine.ParticleSystem testParticleSystem;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject("TestPS");
            testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            system = new ParticleSystemPlaybackSystem(world);
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        [Test]
        public void TriggerRestartOnRestartCountIncrement()
        {
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            component.LastRestartCount = 0;

            var pb = new PBParticleSystem { RestartCount = 1, IsDirty = true };

            Entity entity = world.Create(pb, component);
            system.Update(0);

            ref var updatedComponent = ref world.Get<ParticleSystemComponent>(entity);
            Assert.AreEqual(1u, updatedComponent.LastRestartCount);
        }

        [Test]
        public void PlayWhenPlaybackStatePlaying()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isPlaying);
        }

        [Test]
        public void PauseWhenPlaybackStatePaused()
        {
            testParticleSystem.Play();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsPaused
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isPaused);
        }

        [Test]
        public void StopWhenPlaybackStateStopped()
        {
            testParticleSystem.Play();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsStopped
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isStopped);
        }

        [Test]
        public void DefaultToPlayingWhenPlaybackStateNotSet()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { IsDirty = true };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isPlaying);
        }

        [Test]
        public void PauseAllOnSceneNotCurrent()
        {
            testParticleSystem.Play();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying };

            world.Create(pb, component);
            system.OnSceneIsCurrentChanged(false);

            Assert.IsTrue(testParticleSystem.isPaused);
        }

        [Test]
        public void ResumePlayingOnSceneCurrent()
        {
            testParticleSystem.Pause();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying };

            world.Create(pb, component);
            system.OnSceneIsCurrentChanged(true);

            Assert.IsTrue(testParticleSystem.isPlaying);
        }

        [Test]
        public void NotResumeStoppedOnSceneCurrent()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsStopped };

            world.Create(pb, component);
            system.OnSceneIsCurrentChanged(true);

            Assert.IsTrue(testParticleSystem.isStopped);
        }

        [Test]
        public void SkipPlaybackWhenNotDirtyAndNoRestart()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = false,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isStopped);
        }
    }

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
