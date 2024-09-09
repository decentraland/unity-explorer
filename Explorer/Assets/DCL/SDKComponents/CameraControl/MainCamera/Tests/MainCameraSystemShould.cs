using Arch.Core;
using Cinemachine;
using DCL.SDKComponents.CameraControl.MainCamera.Systems;
using ECS.TestSuite;
using NUnit.Framework;

namespace DCL.SDKComponents.CameraControl.MainCamera.Tests
{
    public class MainCameraSystemShould : UnitySystemTestBase<MainCameraSystem>
    {
        [SetUp]
        public void Setup()
        {

        }

        [TearDown]
        public void Teardown()
        {
            // poolsRegistry.Dispose();
            // Object.DestroyImmediate(virtualCamera.gameObject);
        }

        [Test]
        public void SetupMainCameraComponentCorrectly()
        {

        }

        [Test]
        public void UpdateMainCameraCorrectly()
        {

        }

        [Test]
        public void UpdateOnVirtualCameraLookAtUpdateCorrectly()
        {
            /*uint lookAtEntity1 = 358;
            var component = new PBVirtualCamera()
            {
                LookAtEntity = lookAtEntity1,
                IsDirty = true
            };
            world.Add(entity, component);

            system.Update(1f);
            Assert.IsTrue(world.TryGet(entity, out VirtualCameraComponent vCamComponent));
            Assert.AreEqual(vCamComponent.lookAtCRDTEntity, lookAtEntity1);

            uint lookAtEntity2 = 666;
            component.LookAtEntity = lookAtEntity2;
            component.IsDirty = false;
            world.Set(entity, component);

            system.Update(1f);
            Assert.IsTrue(world.TryGet(entity, out vCamComponent));
            Assert.AreNotEqual(vCamComponent.lookAtCRDTEntity, lookAtEntity2);

            component.IsDirty = true;
            world.Set(entity, component);

            system.Update(1f);
            Assert.IsTrue(world.TryGet(entity, out vCamComponent));
            Assert.AreEqual(vCamComponent.lookAtCRDTEntity, lookAtEntity2);*/
        }

        [Test]
        public void UpdateCinemachineCorrectly()
        {

        }

        [Test]
        public void UpdateMainCameraTransformValuesCorrectly()
        {

        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {

        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {

        }
    }
}
