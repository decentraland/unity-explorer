using Arch.Core;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
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

            var sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);

            system = new MainCameraSystem(world, mainCameraEntity, entitiesMap, sceneStateProvider, cameraData, Substitute.For<ISceneRestrictionBusController>(), globalWorld, sceneData);
        }

        protected override void OnTearDown()
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
        public void SetupCameraComponentCorrectly()
        {
            world.Remove<MainCameraComponent>(mainCameraEntity);
            // Queries in SystemUpdate need the state provider's value
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.IsTrue(world.Has<MainCameraComponent>(mainCameraEntity));
        }

        [Test]
        public void NotSetupComponentWhenNotInCurrentScene()
        {
            world.Remove<MainCameraComponent>(mainCameraEntity);
            // Queries in SystemUpdate need the state provider's value
            sceneStateProvider.IsCurrent.Returns(false);
            SystemUpdate();
            Assert.IsFalse(world.Has<MainCameraComponent>(mainCameraEntity));
        }

        [Test]
        public void SetupComponentWhenEnteringScene()
        {
            world.Remove<MainCameraComponent>(mainCameraEntity);
            // Queries in SystemUpdate need the state provider's value
            sceneStateProvider.IsCurrent.Returns(false);
            SystemUpdate();
            Assert.IsFalse(world.Has<MainCameraComponent>(mainCameraEntity));

            // "Enter scene" by setting state provider
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.IsTrue(world.Has<MainCameraComponent>(mainCameraEntity));
        }

        [Test]
        public void NotSetupComponentIfNotCameraReservedEntity()
        {
            world.Remove<MainCameraComponent>(mainCameraEntity);
            var nonCameraEntity = world.Create(new PBMainCamera());
            // Set state for SystemUpdate queries
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.IsFalse(world.Has<MainCameraComponent>(nonCameraEntity));
        }

        [Test]
        public void UpdateCinemachineCameraCorrectly()
        {
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.IsNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.IsFalse(sdkCinemachineCam1.enabled);

            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.IsTrue(sdkCinemachineCam1.enabled);

            // Release active vCam in MainCameraComponent
            pbMainCameraComponent.ClearVirtualCameraEntity();
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreNotSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // Set virtualCameraEntity2 as active vCam
            pbMainCameraComponent.VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity2).Id;
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsTrue(sdkCinemachineCam2.enabled);
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // Set virtualCameraEntity1 as active vCam again
            pbMainCameraComponent.VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id;
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
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
            sceneStateProvider.IsCurrent.Returns(true);
            SystemUpdate();
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.AreEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Release active vCam in MainCameraComponent
            pbMainCameraComponent.ClearVirtualCameraEntity();
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);
        }

        [Test]
        public void UpdateOnVirtualCameraLookAtChangeCorrectly()
        {
            sceneStateProvider.IsCurrent.Returns(true);

            var pbVirtualCamera = new PBVirtualCamera() { DefaultTransition = new CameraTransition() };
            world.Set(virtualCameraEntity1, pbVirtualCamera);
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
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

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.IsNotNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachineHardLookAt>());
            Assert.IsNotNull(sdkCinemachineCam1.m_LookAt);
            Assert.AreSame(world.Get<TransformComponent>(virtualCameraEntity2).Transform, sdkCinemachineCam1.m_LookAt);

            // Release LookAT
            pbVirtualCamera.ClearLookAtEntity();
            pbVirtualCamera.IsDirty = true;
            world.Set(virtualCameraEntity1, pbVirtualCamera);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.IsNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachineHardLookAt>());
            Assert.IsNotNull(sdkCinemachineCam1.GetRig(1).GetCinemachineComponent<CinemachinePOV>());
            Assert.IsNull(sdkCinemachineCam1.m_LookAt);
            // Releasing LookAt shouldn't switch back to default camera immediately if scene is current
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
        }

        [Test]
        public void HandleEnterAndLeaveSceneCorrectly()
        {
            // Assign vCam while outside scene (scene not current)
            system.OnSceneIsCurrentChanged(false);
            sceneStateProvider.IsCurrent.Returns(false); // Set state for SystemUpdate
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.AreNotSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreNotSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // "enter scene" by setting state provider
            sceneStateProvider.IsCurrent.Returns(true); // Set state for SystemUpdate
            SystemUpdate();
            Assert.IsTrue(sdkCinemachineCam1.enabled);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // change vCam while inside scene
            pbMainCameraComponent.VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity2).Id;
            world.Set(mainCameraEntity, pbMainCameraComponent);
            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsTrue(sdkCinemachineCam2.enabled);
            Assert.AreSame(sdkCinemachineCam2, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // "exit scene" by triggering the listener
            system.OnSceneIsCurrentChanged(false);
            sceneStateProvider.IsCurrent.Returns(false); // Set state for SystemUpdate
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsFalse(sdkCinemachineCam2.enabled);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);

            // "re-enter scene" by setting state provider
            sceneStateProvider.IsCurrent.Returns(true); // Set state for SystemUpdate
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsTrue(sdkCinemachineCam2.enabled);
            Assert.AreSame(sdkCinemachineCam2, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam2.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {
            // Ensure scene is current via state provider only
            sceneStateProvider.IsCurrent.Returns(true);

            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.IsTrue(sdkCinemachineCam1.enabled);
            Assert.AreEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Remove PB component
            world.Remove<PBMainCamera>(mainCameraEntity);
            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsFalse(sdkCinemachineCam2.enabled);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);
        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {
            // Ensure scene is current via state provider only
            sceneStateProvider.IsCurrent.Returns(true);

            // Set virtualCameraEntity1 as active vCam
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsNotNull(world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1, world.Get<MainCameraComponent>(mainCameraEntity).virtualCameraInstance);
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.IsTrue(sdkCinemachineCam1.enabled);
            Assert.AreEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);

            // Add DeleteEntityIntention component
            world.Add<DeleteEntityIntention>(mainCameraEntity);
            sceneStateProvider.IsCurrent.Returns(true); // Keep scene current for update
            SystemUpdate();
            Assert.IsFalse(sdkCinemachineCam1.enabled);
            Assert.IsFalse(sdkCinemachineCam2.enabled);
            Assert.AreSame(defaultCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject);
            Assert.AreNotEqual(CameraMode.SDKCamera, globalWorld.Get<CameraComponent>(globalWorldCameraEntity).Mode);
        }

        [Test]
        public void RetryApplyingVirtualCameraWhenComponentNotInitiallyAvailable()
        {
            sceneStateProvider.IsCurrent.Returns(true);

            // Create a virtual camera entity with CRDT entity but WITHOUT VirtualCameraComponent initially
            var vCamCRDTEntity = new CRDTEntity(555);
            var incompleteVirtualCameraEntity = world.Create(vCamCRDTEntity, new TransformComponent());
            entitiesMap[vCamCRDTEntity] = incompleteVirtualCameraEntity;

            // Set the incomplete virtual camera as active in MainCamera
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)vCamCRDTEntity.Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            // First update should not apply the virtual camera since VirtualCameraComponent is missing
            SystemUpdate();
            var mainCameraComponent = world.Get<MainCameraComponent>(mainCameraEntity);
            Assert.IsNull(mainCameraComponent.virtualCameraCRDTEntity, "Virtual camera CRDT entity should not be set when component is missing");
            Assert.IsNull(mainCameraComponent.virtualCameraInstance, "Virtual camera instance should not be set when component is missing");

            // Now add the missing VirtualCameraComponent and PBVirtualCamera
            var sdkCinemachineCam = new GameObject("RetryTestVirtualCamera").AddComponent<CinemachineFreeLook>();
            sdkCinemachineCam.enabled = false;
            sdkCinemachineCam.transform.position = Vector3.one * 10f;
            var vCamComponent = new VirtualCameraComponent(sdkCinemachineCam, -1);
            var pbVCamComponent = new PBVirtualCamera() { DefaultTransition = new CameraTransition() { Speed = 25 } };

            world.Add(incompleteVirtualCameraEntity, vCamComponent, pbVCamComponent);

            // Second update should now successfully apply the virtual camera
            SystemUpdate();

            mainCameraComponent = world.Get<MainCameraComponent>(mainCameraEntity);
            Assert.IsNotNull(mainCameraComponent.virtualCameraCRDTEntity, "Virtual camera CRDT entity should be set after component is available");
            Assert.AreEqual(vCamCRDTEntity.Id, mainCameraComponent.virtualCameraCRDTEntity.Value.Id, "Virtual camera CRDT entity should match the target entity");
            Assert.IsNotNull(mainCameraComponent.virtualCameraInstance, "Virtual camera instance should be set after component is available");
            Assert.AreSame(sdkCinemachineCam, mainCameraComponent.virtualCameraInstance, "Virtual camera instance should match the created camera");
            Assert.IsTrue(sdkCinemachineCam.enabled, "Virtual camera should be enabled after successful application");
            Assert.AreSame(sdkCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject, "Virtual camera should be the active camera");

            // Cleanup
            GameObject.DestroyImmediate(sdkCinemachineCam.gameObject);
        }

        [Test]
        public void HandleVirtualCameraApplicationFailureCorrectly()
        {
            sceneStateProvider.IsCurrent.Returns(true);

            // Create an initial virtual camera that can be successfully applied
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)world.Get<CRDTEntity>(virtualCameraEntity1).Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            var mainCameraComponent = world.Get<MainCameraComponent>(mainCameraEntity);
            Assert.IsNotNull(mainCameraComponent.virtualCameraCRDTEntity, "Initial virtual camera should be applied successfully");
            Assert.AreSame(sdkCinemachineCam1, mainCameraComponent.virtualCameraInstance);
            Assert.IsTrue(sdkCinemachineCam1.enabled);

            // Now try to switch to a virtual camera that doesn't have required components (will fail TryApplyVirtualCamera)
            var failureCamCRDTEntity = new CRDTEntity(777);
            var incompleteVirtualCameraEntity = world.Create(failureCamCRDTEntity, new TransformComponent());
            entitiesMap[failureCamCRDTEntity] = incompleteVirtualCameraEntity;

            // Set the incomplete virtual camera as the new target
            pbMainCameraComponent.VirtualCameraEntity = (uint)failureCamCRDTEntity.Id;
            world.Set(mainCameraEntity, pbMainCameraComponent);

            // Update should fail to apply the new virtual camera and should NOT assign virtualCameraCRDTEntity
            SystemUpdate();
            mainCameraComponent = world.Get<MainCameraComponent>(mainCameraEntity);

            // The virtualCameraCRDTEntity should still be the previous one (from virtualCameraEntity1)
            Assert.IsNotNull(mainCameraComponent.virtualCameraCRDTEntity, "Virtual camera CRDT entity should retain the previous successful assignment");
            Assert.AreEqual(world.Get<CRDTEntity>(virtualCameraEntity1).Id, mainCameraComponent.virtualCameraCRDTEntity.Value.Id,
                "Virtual camera CRDT entity should still be the previous successful one, not the failed target");
            Assert.AreSame(sdkCinemachineCam1, mainCameraComponent.virtualCameraInstance, "Virtual camera instance should remain unchanged when application fails");
            Assert.IsTrue(sdkCinemachineCam1.enabled, "Previous virtual camera should remain enabled when new application fails");
            Assert.AreSame(sdkCinemachineCam1.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject,
                "Previous virtual camera should remain the active camera when new application fails");
        }

        [Test]
        public void HandleVirtualCameraApplicationSuccessCorrectly()
        {
            sceneStateProvider.IsCurrent.Returns(true);

            // Create a virtual camera entity without components initially
            var vCamCRDTEntity = new CRDTEntity(888);
            var incompleteVirtualCameraEntity = world.Create(vCamCRDTEntity, new TransformComponent());
            entitiesMap[vCamCRDTEntity] = incompleteVirtualCameraEntity;

            // Set the incomplete virtual camera as target - this should fail initially
            var pbMainCameraComponent = new PBMainCamera() { VirtualCameraEntity = (uint)vCamCRDTEntity.Id };
            world.Set(mainCameraEntity, pbMainCameraComponent);

            SystemUpdate();
            var mainCameraComponent = world.Get<MainCameraComponent>(mainCameraEntity);
            Assert.IsNull(mainCameraComponent.virtualCameraCRDTEntity, "Virtual camera CRDT entity should not be set when application fails");
            Assert.IsNull(mainCameraComponent.virtualCameraInstance, "Virtual camera instance should not be set when application fails");

            // Now add the required components to make the virtual camera valid
            var sdkCinemachineCam = new GameObject("SuccessTestVirtualCamera").AddComponent<CinemachineFreeLook>();
            sdkCinemachineCam.enabled = false;
            sdkCinemachineCam.transform.position = Vector3.one * 15f;
            var vCamComponent = new VirtualCameraComponent(sdkCinemachineCam, -1);
            var pbVCamComponent = new PBVirtualCamera() { DefaultTransition = new CameraTransition() { Speed = 30 } };

            world.Add(incompleteVirtualCameraEntity, vCamComponent, pbVCamComponent);

            // Update should now successfully apply the virtual camera
            SystemUpdate();
            mainCameraComponent = world.Get<MainCameraComponent>(mainCameraEntity);

            Assert.IsNotNull(mainCameraComponent.virtualCameraCRDTEntity, "Virtual camera CRDT entity should be set when application succeeds");
            Assert.AreEqual(vCamCRDTEntity.Id, mainCameraComponent.virtualCameraCRDTEntity.Value.Id,
                "Virtual camera CRDT entity should match the successfully applied camera");
            Assert.IsNotNull(mainCameraComponent.virtualCameraInstance, "Virtual camera instance should be set when application succeeds");
            Assert.AreSame(sdkCinemachineCam, mainCameraComponent.virtualCameraInstance, "Virtual camera instance should match the applied camera");
            Assert.IsTrue(sdkCinemachineCam.enabled, "Virtual camera should be enabled after successful application");
            Assert.AreSame(sdkCinemachineCam.gameObject, cinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject,
                "Virtual camera should be the active camera after successful application");

            // Cleanup
            GameObject.DestroyImmediate(sdkCinemachineCam.gameObject);
        }

        private void SystemUpdate()
        {
            system!.Update(1f);
            cinemachineBrain.ManualUpdate();
        }
    }
}
