using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.SDKComponents.CameraControl.MainCamera.Tests
{
    public class VirtualCameraUtilsShould
    {
        private World world;
        private Dictionary<CRDTEntity, Entity> entitiesMap = new Dictionary<CRDTEntity, Entity>();
        private Entity entity1;
        private Entity entity2;

        [SetUp]
        public void Setup()
        {
            world = World.Create();

            // Populate entities map
            CRDTEntity crdtEntity1 = new CRDTEntity(15);
            entity1 = world.Create(crdtEntity1);
            entitiesMap[crdtEntity1] = entity1;

            CRDTEntity crdtEntity2 = new CRDTEntity(635);
            entity2 = world.Create(crdtEntity2);
            entitiesMap[crdtEntity2] = entity2;
        }

        [TearDown]
        public void TearDown()
        {
            entitiesMap.Clear();
            world.Dispose();
        }

        [Test]
        public void FetchVirtualCameraComponentCorrectly()
        {
            world.Add<VirtualCameraComponent>(entity1);

            Assert.IsTrue(VirtualCameraUtils.TryGetVirtualCameraComponent(world, entitiesMap, world.Get<CRDTEntity>(entity1).Id, out var vCamComponent));
            Assert.IsFalse(VirtualCameraUtils.TryGetVirtualCameraComponent(world, entitiesMap, world.Get<CRDTEntity>(entity2).Id, out vCamComponent));
        }

        [Test]
        public void FetchTargetLookAtCRDTEntityCorrectly()
        {
            int virtualCameraCRDTEntity = 189;

            // Empty LookAt prop
            PBVirtualCamera pbComponent = new PBVirtualCamera();
            Assert.AreEqual(-1, VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbComponent, virtualCameraCRDTEntity));

            // Invalid LookAt values
            pbComponent.LookAtEntity = SpecialEntitiesID.CAMERA_ENTITY;
            Assert.AreEqual(-1, VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbComponent, virtualCameraCRDTEntity));

            pbComponent.LookAtEntity = (uint)virtualCameraCRDTEntity;
            Assert.AreEqual(-1, VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbComponent, virtualCameraCRDTEntity));

            // Valid LookAt values
            pbComponent.LookAtEntity = SpecialEntitiesID.PLAYER_ENTITY;
            Assert.AreEqual(SpecialEntitiesID.PLAYER_ENTITY, VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbComponent, virtualCameraCRDTEntity));

            pbComponent.LookAtEntity = 627;
            Assert.AreEqual(627, VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbComponent, virtualCameraCRDTEntity));
        }
    }
}
