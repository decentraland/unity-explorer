using Arch.Core;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using DCL.SDKComponents.CameraControl.MainCamera.Systems;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Tests
{
    public class MainCameraSystemShould : UnitySystemTestBase<MainCameraSystem>
    {
        private Entity mainCameraEntity;
        private Entity virtualCameraEntity1;
        private Entity virtualCameraEntity2;
        private Entity globalWorldCameraEntity;
        private ISceneStateProvider sceneStateProvider;
        private IExposedCameraData cameraData;
        private World globalWorld;
        private Dictionary<CRDTEntity, Entity> entitiesMap = new Dictionary<CRDTEntity, Entity>();
        private CinemachineBrain cinemachineBrain;
        private CinemachineFreeLook sdkCinemachineCam1;
        private CinemachineFreeLook sdkCinemachineCam2;
        private CinemachineFreeLook defaultCinemachineCam;

        [SetUp]
        public void Setup()
        {
            // Create 'main camera' entity
            mainCameraEntity = world.Create(
                PartitionComponent.TOP_PRIORITY,
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                new TransformComponent(),
                new PBMainCamera(),
                new MainCameraComponent());
            entitiesMap[SpecialEntitiesID.CAMERA_ENTITY] = mainCameraEntity;

            // Create 'virtual camera' entities
            sdkCinemachineCam1 = new GameObject("SDKVirtualCamera1").AddComponent<CinemachineFreeLook>();
            sdkCinemachineCam1.enabled = false;
            sdkCinemachineCam1.transform.position = Vector3.one * 12.5f;
            VirtualCameraComponent vCamComponent = new VirtualCameraComponent(sdkCinemachineCam1, -1);
            CRDTEntity vCamCRDTEntity = new CRDTEntity(222);
            virtualCameraEntity1 = world.Create(vCamCRDTEntity, vCamComponent, new TransformComponent(sdkCinemachineCam1.transform),
                new PBVirtualCamera()
                {
                    DefaultTransition = new CameraTransition() { Speed = 30 }
                });
            entitiesMap[vCamCRDTEntity] = virtualCameraEntity1;

            sdkCinemachineCam2 = new GameObject("SDKVirtualCamera1").AddComponent<CinemachineFreeLook>();
            sdkCinemachineCam2.enabled = false;
            sdkCinemachineCam1.transform.position = Vector3.one * -5.3f;
            vCamComponent = new VirtualCameraComponent(sdkCinemachineCam2, -1);
            vCamCRDTEntity = new CRDTEntity(223);
            virtualCameraEntity2 = world.Create(vCamCRDTEntity, vCamComponent, new TransformComponent(sdkCinemachineCam2.transform),
                new PBVirtualCamera()
                {
                    DefaultTransition = new CameraTransition() { Time = 2.5f }
                });
            entitiesMap[vCamCRDTEntity] = virtualCameraEntity2;

            globalWorld = World.Create();
            globalWorldCameraEntity = globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                new CameraComponent { Mode = CameraMode.ThirdPerson }
            );
            var cameraEntityProxy = new ObjectProxy<Entity>();
            cameraEntityProxy.SetObject(globalWorldCameraEntity);

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            cinemachineBrain = new GameObject("CinemachineBrain").AddComponent<CinemachineBrain>();
            defaultCinemachineCam = new GameObject("DefaultCinemachineCam").AddComponent<CinemachineFreeLook>();
            defaultCinemachineCam.enabled = true;
            cinemachineBrain.ManualUpdate();
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            cameraData = Substitute.For<IExposedCameraData>();
            cameraData.CinemachineBrain.Returns(cinemachineBrain);
            cameraData.CameraEntityProxy.Returns(cameraEntityProxy);

            system = new MainCameraSystem(world, mainCameraEntity, entitiesMap, sceneStateProvider, cameraData, globalWorld);
        }

        [TearDown]
        public void Teardown()
        {
            entitiesMap.Clear();
            world.Dispose();
            globalWorld.Dispose();
            GameObject.DestroyImmediate(sdkCinemachineCam1.gameObject);
            GameObject.DestroyImmediate(sdkCinemachineCam2.gameObject);
            GameObject.DestroyImmediate(defaultCinemachineCam.gameObject);
            GameObject.DestroyImmediate(cinemachineBrain.gameObject);
        }

        [Test]
        public void SetupMainCameraComponentCorrectly()
        {
            world.Remove<MainCameraComponent>(mainCameraEntity);

            // Do not set up if not current scene
            sceneStateProvider.IsCurrent.Returns(false);
            SystemUpdate();
            Assert.IsFalse(world.Has<MainCameraComponent>(mainCameraEntity));

            // Set up if current scene
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.IsTrue(world.Has<MainCameraComponent>(mainCameraEntity));

            // Do not set up an entity that's not the main camera entity
            var nonCameraEntity = world.Create(new PBMainCamera());
            SystemUpdate();
            Assert.IsFalse(world.Has<MainCameraComponent>(nonCameraEntity));
        }

        [Test]
        public void UpdateCinemachineCameraCorrectly()
        {
            SystemUpdate();
            Assert.IsNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.IsFalse(sdkCinemachineCam1.enabled);

            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.IsTrue(sdkCinemachineCam1.enabled);

            // Release active vCam in MainCameraComponent
            pbMainCameraComponent.VirtualCameraEntity = 0;
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreNotSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // Set virtualCameraEntity2 as active vCam
            pbMainCameraComponent.VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity2).Id;
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsTrue(sdkCinemachineCam2.enabled);
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // Set virtualCameraEntity1 as active vCam again
            pbMainCameraComponent.VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id;
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam2.enabled);
            Assert.IsTrue(sdkCinemachineCam1.enabled);
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
        }

        [Test]
        public void UpdateGlobalWorldCameraModeCorrectly()
        {
            SystemUpdate();
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.AreEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Release active vCam in MainCameraComponent
            pbMainCameraComponent.VirtualCameraEntity = 0;
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);
        }

        [Test]
        public void UpdateOnVirtualCameraLookAtChangeCorrectly()
        {
            var pbVirtualCamera = new PBVirtualCamera() { DefaultTransition = new CameraTransition() };
            world.Set(virtualCameraEntity1, pbVirtualCamera);
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.IsNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachineHardLookAt>());
            Assert.IsNull(sdkCinemachineCam1.m_LookAt);
            Assert.IsNotNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachinePOV>());

            // Assign LookAT
            uint lookAtCRDTEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity2).Id;
            pbVirtualCamera.LookAtEntity = lookAtCRDTEntity;
            pbVirtualCamera.IsDirty = true;
            world.Set(virtualCameraEntity1, pbVirtualCamera);

            SystemUpdate();
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.IsNotNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachineHardLookAt>());
            Assert.IsNotNull(sdkCinemachineCam1.m_LookAt);
            Assert.AreSame(world.Get<TransformComponent>(virtualCameraEntity2).Transform, sdkCinemachineCam1.m_LookAt);

            // Release LookAT
            pbVirtualCamera.ClearLookAtEntity();
            pbVirtualCamera.IsDirty = true;
            world.Set(virtualCameraEntity1, pbVirtualCamera);

            SystemUpdate();
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.IsNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachineHardLookAt>());
            Assert.IsNotNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachinePOV>());
            Assert.IsNull(sdkCinemachineCam1.m_LookAt);
        }

        [Test]
        public void HandleEnterAndLeaveSceneCorrectly()
        {
            // Assign vCam while outside scene
            sceneStateProvider.IsCurrent.Returns(false);
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.AreNotSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreNotSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // "enter scene"
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.IsTrue(sdkCinemachineCam1.enabled);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // change vCam while inside scene
            pbMainCameraComponent.VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity2).Id;
            world.Set(mainCameraEntity, pbMainCameraComponent);
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsTrue(sdkCinemachineCam2.enabled);
            Assert.AreSame(sdkCinemachineCam2, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // "exit scene"
            sceneStateProvider.IsCurrent.Returns(false);
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsFalse(sdkCinemachineCam2.enabled);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // "re-enter scene"
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsTrue(sdkCinemachineCam2.enabled);
            Assert.AreSame(sdkCinemachineCam2, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {
            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.IsTrue(sdkCinemachineCam1.enabled);
            Assert.AreEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Remove PB component
            world.Remove<PBMainCamera>(mainCameraEntity);
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsFalse(sdkCinemachineCam2.enabled);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);
        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {
            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.IsTrue(sdkCinemachineCam1.enabled);
            Assert.AreEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Add DeleteEntityIntention component
            world.Add<DeleteEntityIntention>(mainCameraEntity);
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsFalse(sdkCinemachineCam2.enabled);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);
        }

        private void SystemUpdate()
        {
            system.Update(1f);
            cinemachineBrain.ManualUpdate();
        }
    }
}
