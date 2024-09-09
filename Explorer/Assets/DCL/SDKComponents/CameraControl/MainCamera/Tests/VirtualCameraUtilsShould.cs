using Arch.Core;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Tests
{
    public class VirtualCameraUtilsShould
    {
        private World world;
        private Dictionary<CRDTEntity, Entity> entitiesMap = new Dictionary<CRDTEntity, Entity>();
        private Entity entity1;
        private GameObject crdtEntity1GO;
        private Entity entity2;
        private GameObject crdtEntity2GO;

        [SetUp]
        public void Setup()
        {
            world = World.Create();

            // Populate entities map
            CRDTEntity crdtEntity1 = new CRDTEntity(15);
            crdtEntity1GO = new GameObject("crdtEntity1");
            crdtEntity1GO.transform.position = Vector3.one * 25f;
            var crdtEntity1Transform = new TransformComponent(crdtEntity1GO);
            entity1 = world.Create(crdtEntity1, crdtEntity1Transform);
            entitiesMap[crdtEntity1] = entity1;

            CRDTEntity crdtEntity2 = new CRDTEntity(635);
            crdtEntity2GO = new GameObject("crdtEntity2");
            crdtEntity2GO.transform.position = Vector3.one * -56f;
            var crdtEntity2Transform = new TransformComponent(crdtEntity2GO);
            entity2 = world.Create(crdtEntity2, crdtEntity2Transform);
            entitiesMap[crdtEntity2] = entity2;
        }

        [TearDown]
        public void TearDown()
        {
            entitiesMap.Clear();
            world.Dispose();
            GameObject.DestroyImmediate(crdtEntity1GO);
            GameObject.DestroyImmediate(crdtEntity2GO);
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

        [Test]
        public void ConfigureCameraTransitionCorrectly()
        {
            // Setup
            CinemachineBrain cinemachineBrain = new GameObject("CinemachineBrain").AddComponent<CinemachineBrain>();
            IExposedCameraData exposedCameraData = Substitute.For<IExposedCameraData>();
            exposedCameraData.CinemachineBrain.Returns(cinemachineBrain);
            int virtualCamCRDTEntity = world.Get<CRDTEntity>(entity1).Id;

            // Time = 0 transition
            PBVirtualCamera pbComponent = new PBVirtualCamera()
            {
                DefaultTransition = new CameraTransition() { Time = 0 }
            };
            world.Add(entity1, pbComponent);
            VirtualCameraUtils.ConfigureVirtualCameraTransition(
                world,
                entitiesMap,
                exposedCameraData,
                virtualCamCRDTEntity,
                3);
            Assert.AreEqual(CinemachineBlendDefinition.Style.Cut, cinemachineBrain.m_DefaultBlend.m_Style);

            // Time = 2.78 transition
            pbComponent.DefaultTransition = new CameraTransition() { Time = 2.78f };
            world.Set(entity1, pbComponent);
            VirtualCameraUtils.ConfigureVirtualCameraTransition(
                world,
                entitiesMap,
                exposedCameraData,
                virtualCamCRDTEntity,
                20);
            Assert.AreEqual(CinemachineBlendDefinition.Style.EaseInOut, cinemachineBrain.m_DefaultBlend.m_Style);
            Assert.AreEqual(pbComponent.DefaultTransition.Time, cinemachineBrain.m_DefaultBlend.m_Time);

            // Time = -1 transition
            pbComponent.DefaultTransition = new CameraTransition() { Time = -1 };
            world.Set(entity1, pbComponent);
            VirtualCameraUtils.ConfigureVirtualCameraTransition(
                world,
                entitiesMap,
                exposedCameraData,
                virtualCamCRDTEntity,
                68);
            Assert.AreEqual(CinemachineBlendDefinition.Style.Cut, cinemachineBrain.m_DefaultBlend.m_Style);

            // Speed = 0 transition
            pbComponent.DefaultTransition = new CameraTransition() { Speed = 0 };
            world.Add(entity1, pbComponent);
            VirtualCameraUtils.ConfigureVirtualCameraTransition(
                world,
                entitiesMap,
                exposedCameraData,
                virtualCamCRDTEntity,
                8);
            Assert.AreEqual(CinemachineBlendDefinition.Style.Cut, cinemachineBrain.m_DefaultBlend.m_Style);

            // Speed = 53 transition
            pbComponent.DefaultTransition = new CameraTransition() { Speed = 53f };
            world.Set(entity1, pbComponent);
            float distanceBetweenCameras = 35;
            VirtualCameraUtils.ConfigureVirtualCameraTransition(
                world,
                entitiesMap,
                exposedCameraData,
                virtualCamCRDTEntity,
                distanceBetweenCameras);
            Assert.AreEqual(CinemachineBlendDefinition.Style.EaseInOut, cinemachineBrain.m_DefaultBlend.m_Style);
            Assert.AreEqual(VirtualCameraUtils.CalculateDistanceBlendTime(distanceBetweenCameras, pbComponent.DefaultTransition.Speed), cinemachineBrain.m_DefaultBlend.m_Time);

            // Speed = -1 transition
            pbComponent.DefaultTransition = new CameraTransition() { Speed = -1 };
            world.Set(entity1, pbComponent);
            VirtualCameraUtils.ConfigureVirtualCameraTransition(
                world,
                entitiesMap,
                exposedCameraData,
                virtualCamCRDTEntity,
                4);
            Assert.AreEqual(CinemachineBlendDefinition.Style.Cut, cinemachineBrain.m_DefaultBlend.m_Style);

            // Cleanup
            GameObject.DestroyImmediate(cinemachineBrain.gameObject);
        }

        [Test]
        public void ConfigureCameraLookAtCorrectly()
        {
            // Setup
            CinemachineFreeLook cinemachineCamera = new GameObject("CinemachineFreeLookCam").AddComponent<CinemachineFreeLook>();
            var middleRigCamera = cinemachineCamera.GetRig(1);
            VirtualCameraComponent component = new VirtualCameraComponent(cinemachineCamera, world.Get<CRDTEntity>(entity1).Id);

            // LookAt entity1
            VirtualCameraUtils.ConfigureCameraLookAt(world, entitiesMap, component);
            Assert.AreSame(world.Get<TransformComponent>(entity1).Transform, cinemachineCamera.m_LookAt);
            Assert.IsNotNull(middleRigCamera.GetCinemachineComponent<CinemachineHardLookAt>());
            Assert.IsNull(middleRigCamera.GetCinemachineComponent<CinemachinePOV>());

            // LookAt invalid entity
            component.lookAtCRDTEntity = 998;
            VirtualCameraUtils.ConfigureCameraLookAt(world, entitiesMap, component);
            Assert.IsNull(cinemachineCamera.m_LookAt);
            Assert.IsNotNull(middleRigCamera.GetCinemachineComponent<CinemachinePOV>());
            Assert.IsNull(middleRigCamera.GetCinemachineComponent<CinemachineHardLookAt>());

            // LookAt entity2
            component.lookAtCRDTEntity = world.Get<CRDTEntity>(entity2).Id;
            VirtualCameraUtils.ConfigureCameraLookAt(world, entitiesMap, component);
            Assert.AreSame(world.Get<TransformComponent>(entity2).Transform, cinemachineCamera.m_LookAt);
            Assert.IsNotNull(middleRigCamera.GetCinemachineComponent<CinemachineHardLookAt>());
            Assert.IsNull(middleRigCamera.GetCinemachineComponent<CinemachinePOV>());

            // LookAt entity1 after moving it too close to the camera
            world.Get<TransformComponent>(entity1).Transform.position = cinemachineCamera.transform.position + (Vector3.one * 0.1f);
            component.lookAtCRDTEntity = world.Get<CRDTEntity>(entity1).Id;
            VirtualCameraUtils.ConfigureCameraLookAt(world, entitiesMap, component);
            Assert.IsNull(cinemachineCamera.m_LookAt);
            Assert.IsNotNull(middleRigCamera.GetCinemachineComponent<CinemachinePOV>());
            Assert.IsNull(middleRigCamera.GetCinemachineComponent<CinemachineHardLookAt>());

            // Cleanup
            GameObject.DestroyImmediate(cinemachineCamera.gameObject);
        }
    }
}
