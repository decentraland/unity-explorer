using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem;
using DCL.SDKComponents.ParticleSystem.Systems;
using DCL.WebRequests;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Pool;
using Entity = Arch.Core.Entity;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using TextureWrapMode = UnityEngine.TextureWrapMode;

namespace DCL.ParticleSystem.Tests
{
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
                SizeOverTime = new FloatRange { Start = 0.5f, End = 2f }
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

        [Test]
        public void ApplyEmissionBursts()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                Bursts = { new PBParticleSystem.Types.Burst { Time = 0f, Count = 10 } }
            };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.AreEqual(1, testParticleSystem.emission.burstCount);
        }

        [Test]
        public void ClearBurstsWhenEmpty()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.AreEqual(0, testParticleSystem.emission.burstCount);
        }

        [Test]
        public void ApplyBillboardModeByDefault()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var renderer = testParticleSystem.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(ParticleSystemRenderMode.Billboard, renderer.renderMode);
            Assert.AreEqual(ParticleSystemRenderSpace.View, renderer.alignment);
        }

        [Test]
        public void ApplyMeshModeWhenBillboardFalse()
        {
            var pb = new PBParticleSystem { IsDirty = true, Billboard = false };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var renderer = testParticleSystem.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(ParticleSystemRenderMode.Mesh, renderer.renderMode);
            Assert.AreEqual(ParticleSystemRenderSpace.Local, renderer.alignment);
        }

        [Test]
        public void ApplyQuaternionInitialRotationAs3DStartRotation()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                InitialRotation = new Decentraland.Common.Quaternion { X = 0, Y = 0.7071068f, Z = 0, W = 0.7071068f }
            };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.main.startRotation3D);
        }

        [Test]
        public void ApplyBoxShapeWithSize()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                Box = new PBParticleSystem.Types.Box
                {
                    Size = new Decentraland.Common.Vector3 { X = 2f, Y = 3f, Z = 4f }
                }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.AreEqual(ParticleSystemShapeType.Box, testParticleSystem.shape.shapeType);
            Assert.AreEqual(2f, testParticleSystem.shape.scale.x);
            Assert.AreEqual(3f, testParticleSystem.shape.scale.y);
            Assert.AreEqual(4f, testParticleSystem.shape.scale.z);
        }

        [Test]
        public void ApplyColorOverLifetimeGradient()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                ColorOverTime = new Decentraland.Common.ColorRange
                {
                    Start = new Decentraland.Common.Color4 { R = 1f, G = 0f, B = 0f, A = 1f },
                    End = new Decentraland.Common.Color4 { R = 0f, G = 0f, B = 1f, A = 0f }
                }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.colorOverLifetime.enabled);
        }

        [Test]
        public void ApplyForceOverLifetime()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                AdditionalForce = new Decentraland.Common.Vector3 { X = 1f, Y = 2f, Z = 3f }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.forceOverLifetime.enabled);
        }

        [Test]
        public void ApplyLimitVelocityOverLifetime()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                LimitVelocity = new PBParticleSystem.Types.LimitVelocity { Speed = 5f }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.limitVelocityOverLifetime.enabled);
            Assert.AreEqual(5f, testParticleSystem.limitVelocityOverLifetime.limit.constant);
        }

        [Test]
        public void ApplySpriteSheetAnimation()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                SpriteSheet = new PBParticleSystem.Types.SpriteSheetAnimation { TilesX = 4, TilesY = 4 }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.textureSheetAnimation.enabled);
            Assert.AreEqual(4, testParticleSystem.textureSheetAnimation.numTilesX);
            Assert.AreEqual(4, testParticleSystem.textureSheetAnimation.numTilesY);
        }

        [Test]
        public void DisableSpriteSheetWhenNull()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsFalse(testParticleSystem.textureSheetAnimation.enabled);
        }

        [Test]
        public void DisableLimitVelocityWhenNull()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsFalse(testParticleSystem.limitVelocityOverLifetime.enabled);
        }

        [Test]
        public void ApplyInitialSizeRange()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                InitialSize = new FloatRange { Start = 0.5f, End = 2f }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var startSize = testParticleSystem.main.startSize;
            Assert.AreEqual(ParticleSystemCurveMode.TwoConstants, startSize.mode);
            Assert.AreEqual(0.5f, startSize.constantMin);
            Assert.AreEqual(2f, startSize.constantMax);
        }

        [Test]
        public void ApplyInitialVelocitySpeedRange()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                InitialVelocitySpeed = new FloatRange { Start = 1f, End = 5f }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var startSpeed = testParticleSystem.main.startSpeed;
            Assert.AreEqual(ParticleSystemCurveMode.TwoConstants, startSpeed.mode);
            Assert.AreEqual(1f, startSpeed.constantMin);
            Assert.AreEqual(5f, startSpeed.constantMax);
        }

        [Test]
        public void ReuseCachedBurstArrayOnSubsequentDirtyUpdates()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                Bursts = { new PBParticleSystem.Types.Burst { Time = 0f, Count = 5 } }
            };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            Entity entity = world.Create(pb, component);
            system.Update(0);

            ref var comp = ref world.Get<ParticleSystemComponent>(entity);
            var firstArray = comp.CachedBursts;
            Assert.IsNotNull(firstArray);

            ref var pbRef = ref world.Get<PBParticleSystem>(entity);
            pbRef.IsDirty = true;
            system.Update(0);

            ref var comp2 = ref world.Get<ParticleSystemComponent>(entity);
            Assert.AreSame(firstArray, comp2.CachedBursts);
        }

        [Test]
        public void ApplyDefaultInitialSizeWhenNotSet()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var startSize = testParticleSystem.main.startSize;
            Assert.AreEqual(ParticleSystemCurveMode.Constant, startSize.mode);
            Assert.AreEqual(1f, startSize.constant);
        }

        [Test]
        public void ApplyDefaultInitialColorWhenNotSet()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var startColor = testParticleSystem.main.startColor;
            Assert.AreEqual(ParticleSystemGradientMode.Color, startColor.mode);
            Assert.AreEqual(Color.white, startColor.color);
        }

        [Test]
        public void ApplyDefaultInitialSpeedWhenNotSet()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            var startSpeed = testParticleSystem.main.startSpeed;
            Assert.AreEqual(ParticleSystemCurveMode.Constant, startSpeed.mode);
            Assert.AreEqual(1f, startSpeed.constant);
        }

        [Test]
        public void DisableStartRotation3DWhenNoRotationSet()
        {
            var pb = new PBParticleSystem { IsDirty = true };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsFalse(testParticleSystem.main.startRotation3D);
        }

        [Test]
        public void ApplyInitialColorRange()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                InitialColor = new ColorRange
                {
                    Start = new Color4 { R = 1f, G = 0f, B = 0f, A = 1f },
                    End = new Color4 { R = 0f, G = 0f, B = 1f, A = 1f }
                }
            };

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.AreEqual(ParticleSystemGradientMode.TwoColors, testParticleSystem.main.startColor.mode);
        }

        [Test]
        public void ApplyQuaternionRotationOverLifetimeWithSeparateAxes()
        {
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                RotationOverTime = new Decentraland.Common.Quaternion { X = 0, Y = 0, Z = 0, W = 1 }
            };
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.rotationOverLifetime.enabled);
            Assert.IsTrue(testParticleSystem.rotationOverLifetime.separateAxes);
        }

        [Test]
        public void CleanUpTextureWhenDirtyUpdateHasEmptyTextureSrc()
        {
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            var intention = new GetTextureIntention("test-url", "test-hash",
                TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "test");

            component.TexturePromise = TexturePromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            var cancellationToken = component.TexturePromise.Value.LoadingIntention.CancellationTokenSource.Token;

            // First update: has a texture loaded
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                Texture = new Decentraland.Common.Texture { Src = "" }
            };

            Entity entity = world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(cancellationToken.IsCancellationRequested);
            Assert.IsNull(world.Get<ParticleSystemComponent>(entity).TexturePromise);
        }

        [Test]
        public void CleanUpTextureWhenDirtyUpdateHasNullTexture()
        {
            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);

            var intention = new GetTextureIntention("test-url", "test-hash",
                TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "test");

            component.TexturePromise = TexturePromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            var cancellationToken = component.TexturePromise.Value.LoadingIntention.CancellationTokenSource.Token;

            var pb = new PBParticleSystem { IsDirty = true };

            Entity entity = world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(cancellationToken.IsCancellationRequested);
            Assert.IsNull(world.Get<ParticleSystemComponent>(entity).TexturePromise);
        }
    }
}
