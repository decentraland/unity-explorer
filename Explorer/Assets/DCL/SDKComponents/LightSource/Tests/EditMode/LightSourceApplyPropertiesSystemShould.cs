using DCL.ECSComponents;
using DCL.SDKComponents.LightSource.Systems;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.LightSource.Tests
{
    public class LightSourceApplyPropertiesSystemShould : UnitySystemTestBase<LightSourceApplyPropertiesSystem>
    {
        private LightSourceSettings settings = null!;
        private GameObject lightGameObject = null!;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<LightSourceSettings>();
            lightGameObject = new GameObject("TestLight");

            system = new LightSourceApplyPropertiesSystem(world, Substitute.For<ISceneData>(), Substitute.For<IPartitionComponent>(), settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (lightGameObject != null)
                Object.DestroyImmediate(lightGameObject);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void ApplyActiveStateToLiveLightSource()
        {
            Light light = lightGameObject.AddComponent<Light>();
            light.enabled = false;

            world.Create(new PBLightSource { Active = true, Spot = new PBLightSource.Types.Spot() }, new LightSourceComponent(light));

            system.Update(0);

            Assert.IsTrue(light.enabled);
        }

        [Test]
        public void SkipReleasedInstancesOnEntitiesPendingDeletion()
        {
            // The pooled Light is released while the component still sits on the entity until the deferred destruction runs (#10044).
            Light light = lightGameObject.AddComponent<Light>();

            world.Create(new PBLightSource { IsDirty = true, Spot = new PBLightSource.Types.Spot() }, new LightSourceComponent(light), new DeleteEntityIntention());
            Object.DestroyImmediate(lightGameObject);

            Assert.DoesNotThrow(() => system.Update(0));
        }
    }
}
